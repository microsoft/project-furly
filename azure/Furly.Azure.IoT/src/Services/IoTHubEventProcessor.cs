// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Utils;
    using global::Azure.Identity;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Processor;
    using global::Azure.Storage.Blobs;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of event consumer for single node consumption.
    /// </summary>
    public sealed class IoTHubEventProcessor : IIoTHubEventProcessor, IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Create host wrapper
        /// </summary>
        /// <param name="options"></param>
        /// <param name="service"></param>
        /// <param name="storage"></param>
        /// <param name="logger"></param>
        /// <param name="timeProvider"></param>
        public IoTHubEventProcessor(IOptions<IoTHubEventProcessorOptions> options,
            IOptions<IoTHubServiceOptions> service, IOptions<StorageOptions> storage,
            ILogger<IoTHubEventProcessor> logger, TimeProvider? timeProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timeProvider = timeProvider ?? TimeProvider.System;
            _cts = new CancellationTokenSource();

            var blobUri = ProcessOptions(options.Value, service.Value,
                storage.Value, out var eventHubCs, out var ns,
                out var eventHub, out var consumerGroup, out var storageCs,
                out var connectionOptions);
            if (blobUri == null)
            {
                _logger.LogInformation("No storage configured. Using consumer " +
                    "client to read events from all partitions.");

                // Consumer client
                var consumerOptions = new EventHubConsumerClientOptions
                {
                    ConnectionOptions = connectionOptions
                };
                if (eventHubCs != null)
                {
                    _client = new EventHubConsumerClient(consumerGroup,
                        eventHubCs, eventHub, consumerOptions);
                }
                else
                {
                    _client = new EventHubConsumerClient(consumerGroup,
                        ns, eventHub,
                        new DefaultAzureCredential(service.Value.AllowInteractiveLogin),
                        consumerOptions);
                }
            }
            else
            {
                var blobClient = storageCs != null ?
                    new BlobContainerClient(storageCs, blobUri.PathAndQuery) :
                    new BlobContainerClient(blobUri,
                        new DefaultAzureCredential(service.Value.AllowInteractiveLogin));

                blobClient.CreateIfNotExists();

                var processorOptions = new EventProcessorClientOptions
                {
                    LoadBalancingStrategy = LoadBalancingStrategy.Greedy,
                    ConnectionOptions = connectionOptions
                };

                // Processor client
                if (eventHubCs != null)
                {
                    _processor = new EventProcessorClient(blobClient, consumerGroup,
                        eventHubCs, eventHub, processorOptions);
                }
                else
                {
                    _processor = new EventProcessorClient(blobClient, consumerGroup,
                        ns, eventHub,
                        new DefaultAzureCredential(service.Value.AllowInteractiveLogin),
                        processorOptions);
                }
            }
            _task = Task.Factory.StartNew(() => RunAsync(_cts.Token), _cts.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <inheritdoc/>
        public IDisposable Register(IIoTHubTelemetryHandler listener)
        {
            if (!_handlers.TryAdd(listener, true))
            {
                throw new ArgumentException("Failed to add consumer.");
            }
            return new Disposable(() => _handlers.TryRemove(listener, out _));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _cts.CancelAsync().ConfigureAwait(false);
                if (!_task.IsCompleted)
                {
                    await Try.Async(() => _task).ConfigureAwait(false);
                }
            }
            finally
            {
                if (_client != null)
                {
                    await _client.DisposeAsync().ConfigureAwait(false);
                }
                _cts.Dispose();
            }
        }

        /// <summary>
        /// Handle partition opening
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private Task PartitionOpeningAsync(PartitionInitializingEventArgs arg)
        {
            if (arg.CancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }
            _logger.LogInformation("Partition {PartitionId} opened",
                arg.PartitionId);
            if (!_options.Value.InitialReadFromStart ||
                _options.Value.SkipEventsOlderThan != null)
            {
                var start = _timeProvider.GetUtcNow();
                if (_options.Value.SkipEventsOlderThan != null)
                {
                    start -= _options.Value.SkipEventsOlderThan.Value;
                }
                arg.DefaultStartingPosition = EventPosition.FromEnqueuedTime(start);
            }
            else
            {
                arg.DefaultStartingPosition = EventPosition.Earliest;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle partition closing
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private Task PartitionClosingAsync(PartitionClosingEventArgs arg)
        {
            _logger.LogInformation("Partition {PartitionId} closed ({Reason})",
                arg.PartitionId, arg.Reason switch
                {
                    ProcessingStoppedReason.OwnershipLost =>
                        "Another processor claimed ownership",
                    ProcessingStoppedReason.Shutdown =>
                        "The processor is shutting down",
                    _ => arg.Reason.ToString()
                });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle errors
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task ProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            if (arg.CancellationToken.IsCancellationRequested)
            {
                return;
            }
            _logger.LogWarning(arg.Exception,
                "Partition {PartitionId} error during {Operation}",
                arg.PartitionId, arg.Operation);

            Debug.Assert(_processor != null);
            if (!_processor.IsRunning && !_cts.IsCancellationRequested)
            {
                // To be safe, request that processing stop before
                // requesting the start; this will ensure that any
                // processor state is fully reset.

                await _processor.StopProcessingAsync().ConfigureAwait(false);
                await _processor.StartProcessingAsync(
                    _cts.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Process event
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task ProcessEventAsync(ProcessEventArgs arg)
        {
            if (arg.CancellationToken.IsCancellationRequested || !arg.HasEvent)
            {
                return;
            }
            await ProcessEventAsync(arg.Data, arg.CancellationToken).ConfigureAwait(false);

            var checkpointer = _checkpointer.GetOrAdd(arg.Partition.PartitionId,
                _ => new IntervalCheckpoint(_options.Value.CheckpointInterval?.TotalMilliseconds));
            if (arg.CancellationToken.IsCancellationRequested || checkpointer.ShouldCheckpoint)
            {
                _logger.LogDebug("Checkpointing for partition {PartitionId}...",
                    arg.Partition.PartitionId);
                await arg.UpdateCheckpointAsync().ConfigureAwait(false);
                checkpointer.CheckpointComplete();
                arg.CancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Run the event processor host
        /// </summary>
        /// <returns></returns>
        public async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_client != null)
                {
                    try
                    {
                        var reader = _client.ReadEventsAsync(ct);
                        await foreach (var ev in reader.ConfigureAwait(false))
                        {
                            await ProcessEventAsync(ev.Data, ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read or process events");
                    }
                }
                else
                {
                    Debug.Assert(_processor != null);
                    try
                    {
                        _processor.ProcessEventAsync += ProcessEventAsync;
                        _processor.ProcessErrorAsync += ProcessErrorAsync;
                        _processor.PartitionClosingAsync += PartitionClosingAsync;
                        _processor.PartitionInitializingAsync += PartitionOpeningAsync;
                        try
                        {
                            // Once processing has started, the delay will
                            // block to allow processing until cancellation
                            // is requested.
                            await _processor.StartProcessingAsync(ct).ConfigureAwait(false);
                            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to start processing events.");
                        }
                        finally
                        {
                            // This may take up to the length of time defined
                            // as part of the configured TryTimeout of the processor;
                            // by default, this is 60 seconds.
                            await _processor.StopProcessingAsync(default).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _processor.ProcessEventAsync -= ProcessEventAsync;
                        _processor.ProcessErrorAsync -= ProcessErrorAsync;
                        _processor.PartitionClosingAsync -= PartitionClosingAsync;
                        _processor.PartitionInitializingAsync -= PartitionOpeningAsync;
                    }
                }
            }
        }

        /// <summary>
        /// Process event data
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ProcessEventAsync(EventData eventData,
            CancellationToken ct)
        {
            const string kIoTHubContentType = "iothub-content-type";
            const string kIoTHubContentEncoding = "iothub-content-encoding";
            const string kIoTHubDeviceId = "iothub-connection-device-id";
            const string kIoTHubModuleId = "iothub-connection-module-id";
            const string kDeviceId = "deviceId";
            const string kModuleId = "moduleId";
            const string kIoTHubMessageSchema = "iothub-message-schema";
            const string kEventHubContentType = "content-type";
            const string kEventHubContentEncoding = "content-encoding";
            const string kTo = "to";

            var properties = new EventProperties(
                eventData.SystemProperties,
                eventData.Properties);
            if (eventData == null)
            {
                _logger.LogTrace(
                    "WARNING: Received empty message with {@properties}",
                    properties);
                return;
            }
            if (!properties.TryGetValue(kIoTHubDeviceId, out var deviceId) &&
                !properties.TryGetValue(kDeviceId, out deviceId))
            {
                // Not from a device
                return;
            }

            if (!properties.TryGetValue(kIoTHubModuleId, out var moduleId) &&
                !properties.TryGetValue(kModuleId, out moduleId))
            {
                // Not from a module
                moduleId = null;
            }

            if (!properties.TryGetValue(kTo, out var target) &&
                !properties.TryGetValue(kIoTHubMessageSchema, out target))
            {
                target = HubResource.Format(null, deviceId, moduleId);
            }

            if (!properties.TryGetValue(kEventHubContentType, out var contentType) &&
                !properties.TryGetValue(kIoTHubContentType, out contentType))
            {
                contentType = ContentMimeType.Json;
            }

            if (!properties.TryGetValue(kEventHubContentEncoding, out var contentEncoding) &&
                !properties.TryGetValue(kIoTHubContentEncoding, out contentEncoding))
            {
                contentEncoding = Encoding.UTF8.WebName;
            }

            var data = eventData.EventBody.ToArray();
            var handlers = _handlers.Keys
                 .Select(consumer => consumer.HandleAsync(deviceId, moduleId,
                 target, new ReadOnlySequence<byte>(data), contentType, contentEncoding,
                 properties, ct).AsTask());
            await Task.WhenAll(handlers).ConfigureAwait(false);
        }

        /// <summary>
        /// Process options to get input for the clients
        /// </summary>
        /// <param name="options"></param>
        /// <param name="service"></param>
        /// <param name="storage"></param>
        /// <param name="cs"></param>
        /// <param name="ns"></param>
        /// <param name="eventHub"></param>
        /// <param name="consumerGroup"></param>
        /// <param name="storageCs"></param>
        /// <param name="connectionOptions"></param>
        /// <returns></returns>
        /// <exception cref="InvalidConfigurationException"></exception>
        private Uri? ProcessOptions(IoTHubEventProcessorOptions options,
            IoTHubServiceOptions service, StorageOptions storage,
            out string? cs, out string ns, out string eventHub,
            out string consumerGroup, out string? storageCs,
            out EventHubConnectionOptions connectionOptions)
        {
            if (string.IsNullOrEmpty(service.ConnectionString))
            {
                throw new InvalidConfigurationException(
                    "No IoT Hub connection string was configured.");
            }
            if (!ConnectionString.TryParse(service.ConnectionString,
                out var connectionString) ||
                connectionString.HubName == null)
            {
                throw new InvalidConfigurationException(
                   "Invalid IoT Hub connection string was configured.");
            }
            var ep = options.EventHubEndpoint;
            if (string.IsNullOrEmpty(ep))
            {
                ep = connectionString.Endpoint;
                if (string.IsNullOrEmpty(ep))
                {
                    throw new InvalidConfigurationException(
                       "No Event hub endpoint was configured.");
                }
            }
            try
            {
                ns = ep;
                eventHub = connectionString.HubName;
                consumerGroup = string.IsNullOrEmpty(options.ConsumerGroup) ?
                    EventHubConsumerClient.DefaultConsumerGroupName :
                    options.ConsumerGroup;
                _logger.LogInformation("Using Consumer Group {ConsumerGroup}",
                    consumerGroup);

                // Create connection string otherwise use azure credentials
                cs = default;
                if (connectionString.SharedAccessKeyName != null &&
                    connectionString.SharedAccessKey != null)
                {
                    cs = ConnectionString.CreateEventHubConnectionString(ep,
                        connectionString.SharedAccessKeyName,
                        connectionString.SharedAccessKey).ToString();
                }

                connectionOptions = new EventHubConnectionOptions();
                if (options.UseWebsockets)
                {
                    connectionOptions.Proxy = HttpClient.DefaultProxy;
                    connectionOptions.TransportType =
                        EventHubsTransportType.AmqpWebSockets;
                }

                storageCs = null;
                var account = storage.AccountName;
                if (string.IsNullOrEmpty(account))
                {
                    return null;
                }
                var key = storage.AccountKey;
                var suffix = storage.EndpointSuffix ?? "core.windows.net";
                if (!string.IsNullOrEmpty(key))
                {
                    storageCs = ConnectionString.CreateStorageConnectionString(
                        account, suffix, key, "https").ToString();
                }
                var containerName = "eh" + Encoding.UTF8.GetBytes(eventHub)
                    .ToSha256Hash()[..32];
                return new UriBuilder()
                {
                    Scheme = "https",
                    Host = $"{account}.blob.{suffix}",
                    Path = containerName
                }.Uri;
            }
            catch (Exception ex)
            {
                throw new InvalidConfigurationException(
                    "Invalid options configured.", ex);
            }
        }

        /// <summary>
        /// Disposable wrapper
        /// </summary>
        private sealed class Disposable : IDisposable
        {
            /// <summary>
            /// Create disposable
            /// </summary>
            /// <param name="disposable"></param>
            public Disposable(Action disposable)
            {
                _disposable = disposable;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _disposable?.Invoke();
            }

            private readonly Action _disposable;
        }

        /// <summary>
        /// Checkpoint each partition
        /// </summary>
        private interface ICheckpointer
        {
            /// <summary>
            /// Ready to checkpoint
            /// </summary>
            bool ShouldCheckpoint { get; }

            /// <summary>
            /// Checkpoint completed
            /// </summary>
            void CheckpointComplete();
        }

        /// <summary>
        /// Check points at an interval
        /// </summary>
        private sealed class IntervalCheckpoint : ICheckpointer
        {
            /// <inheritdoc/>
            public bool ShouldCheckpoint =>
                _sw.ElapsedMilliseconds == 0 || _sw.ElapsedMilliseconds >= _interval;

            /// <summary>
            /// Create checkpointer
            /// </summary>
            /// <param name="interval"></param>
            public IntervalCheckpoint(double? interval)
            {
                _interval = (long?)interval ?? 5000;
            }

            /// <inheritdoc/>
            public void CheckpointComplete()
            {
                _sw.Restart(); // Start or restart the timer
            }

            private readonly long _interval;
            private readonly Stopwatch _sw = new(); // Initially not running
        }

        /// <summary>
        /// Wraps the properties into a string dictionary
        /// </summary>
        private class EventProperties : IReadOnlyDictionary<string, string?>
        {
            /// <inheritdoc/>
            public IEnumerable<string> Keys => _system.Keys
                .Concat(_user.Keys);

            /// <inheritdoc/>
            public IEnumerable<string?> Values => _system.Values
                .Select(v => v?.ToString())
                .Concat(_user.Values.Select(v => v?.ToString()));

            /// <inheritdoc/>
            public int Count => _system.Count + _user.Count;

            /// <inheritdoc/>
            public string? this[string key]
            {
                get
                {
                    TryGetValue(key, out var value);
                    return value;
                }
            }

            /// <summary>
            /// Create properties wrapper
            /// </summary>
            /// <param name="system"></param>
            /// <param name="user"></param>
            internal EventProperties(IReadOnlyDictionary<string, object> system,
                IDictionary<string, object> user)
            {
                _system = system ?? new Dictionary<string, object>();
                _user = user ?? new Dictionary<string, object>();
            }

            /// <inheritdoc/>
            public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                if (_user.TryGetValue(key, out var result) ||
                    _system.TryGetValue(key, out result))
                {
                    value = result?.ToString();
                    return value != null;
                }
                value = null;
                return false;
            }

            /// <inheritdoc/>
            public bool ContainsKey(string key)
            {
                return TryGetValue(key, out _);
            }

            /// <inheritdoc/>
            public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, string?>>)_system.Concat(_user)).GetEnumerator();
            }

            /// <inheritdoc/>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return _system.Concat(_user).GetEnumerator();
            }

            private readonly IReadOnlyDictionary<string, object> _system;
            private readonly IDictionary<string, object> _user;
        }

        private readonly Task _task;
        private readonly ILogger _logger;
        private readonly IOptions<IoTHubEventProcessorOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly EventHubConsumerClient? _client;
        private readonly EventProcessorClient? _processor;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<IIoTHubTelemetryHandler, bool> _handlers = new();
        private readonly ConcurrentDictionary<string, ICheckpointer> _checkpointer = new();
    }
}
