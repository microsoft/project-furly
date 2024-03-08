// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Messaging;
    using Furly.Exceptions;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Extensions.ManagedClient;
    using MQTTnet.Formatter;
    using MQTTnet.Packets;
    using MQTTnet.Protocol;
    using MQTTnet.Server;
    using Nito.AsyncEx;
    using Nito.Disposables;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Security;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Mqtt event client
    /// </summary>
    public sealed class MqttClient : MqttRpcBase, IEventClient, IEventSubscriber,
        IDisposable, IAsyncDisposable, IAwaitable<MqttClient>
    {
        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => MaxMethodPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity { get; }

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public MqttClient(IOptions<MqttOptions> options, ILogger<MqttClient> logger)
            : base(options, logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Identity = _options.Value.ClientId ?? Guid.NewGuid().ToString();
            var numberofPartitions = _options.Value.NumberOfClientPartitions ?? 0;
            _clients = Enumerable
                .Range(0, numberofPartitions == 0 ? 1 : numberofPartitions)
                .Select(_ =>
                {
                    var client = new MqttFactory().CreateManagedMqttClient();

                    client.ConnectedAsync += HandleClientConnectedAsync;
                    client.ConnectingFailedAsync += HandleClientConnectingFailed;
                    client.ConnectionStateChangedAsync += HandleClientConnectionStateChanged;
                    client.SynchronizingSubscriptionsFailedAsync += HandleClientSynchronizingFailed;
                    client.DisconnectedAsync += HandleClientDisconnectedAsync;
                    client.ApplicationMessageProcessedAsync += HandleMessagePublished;
                    client.ApplicationMessageSkippedAsync += HandleMessageSkipped;
                    client.ApplicationMessageReceivedAsync += HandleMessageAsync;

                    return client;
                })
                .ToArray();

            _cts = new CancellationTokenSource();
            _connection = Task.WhenAll(_clients
                .Select((c, i) => c.StartAsync(GetClientOptions(i))));
            _subscriber = Task.Factory.StartNew(() => SubscribeAsync(_cts.Token),
                _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <inheritdoc/>
        public IAwaiter<MqttClient> GetAwaiter()
        {
            return _connection.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            _logger.LogDebug("Closing {ClientId} ...", _options.Value.ClientId);

            // Stop subscriber
            try
            {
                Close();
                await _cts.CancelAsync().ConfigureAwait(false);
                await _subscriber.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop subscriber.");
            }

            // Stop client
            try
            {
                await Task.WhenAll(_clients
                    .Select(c => c.StopAsync())).ConfigureAwait(false);

                _logger.LogDebug(
                    "Clients for client id {ClientId} closed successfully.",
                    _options.Value.ClientId);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop managed client.");
            }
            finally
            {
                _clients.ForEach(c => c.Dispose());
                _subscriptionsLock.Dispose();
                _cts.Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
        {
            if (!TopicFilter.IsValid(topic))
            {
                throw new ArgumentException("Invalid topic filter", nameof(topic));
            }
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Register subscriptions
            IAsyncDisposable result;
            var subscribe = false;
            await _subscriptionsLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(topic, out var consumers))
                {
                    consumers = new List<IEventConsumer> { consumer };
                    subscribe = true;
                    _subscriptions.Add(topic, consumers);
                }
                else
                {
                    consumers.Add(consumer);
                }
                result = new AsyncDisposable(() => UnsubscribeAsync(topic, consumer));
            }
            finally
            {
                _subscriptionsLock.Release();
            }

            //
            // Subscribe to the topic on a connected client, we let that happen on the
            // subscriber thread rather than using the managed client because we need
            // to always ensure that the subscriber is subscribed before the client
            // sends the first message here, specifically for the rpc case.
            //
            if (subscribe)
            {
                var filter = new MqttTopicFilter
                {
                    Topic = topic,
                    QualityOfServiceLevel = (MqttQualityOfServiceLevel)
                        (_options.Value.QoS ?? QoS.AtMostOnce)
                };
                try
                {
                    var tcs = new TaskCompletionSource();
                    _topics.Enqueue((tcs, filter));
                    _triggerSubscriber.Set();
                    await tcs.Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Client {ClientId} failed to subscribe to {Topic}.",
                         _options.Value.ClientId, topic);
                    throw;
                }
            }
            return result;
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return new MqttMessage(_options, PublishAsync);
        }

        /// <inheritdoc/>
        protected override ValueTask<IAsyncDisposable> SubscribeAsync(string topicFilter,
            CancellationToken ct)
        {
            return SubscribeAsync(topicFilter, IEventConsumer.Null, ct);
        }

        /// <inheritdoc/>
        protected override async ValueTask PublishAsync(MqttApplicationMessage message,
            CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();
            var client = await GetClientAsync(message.Topic).ConfigureAwait(false);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _inflight.TryAdd(message, tcs);
            try
            {
                await client.EnqueueAsync(message).ConfigureAwait(false);
            }
            catch
            {
                _inflight.TryRemove(message, out _);
                throw;
            }
            await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Handle connection
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientConnectedAsync(MqttClientConnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _logger.LogInformation("Client connected with {Result} as {ClientId}.",
                args.ConnectResult.ResultCode, _options.Value.ClientId);
            _triggerSubscriber.Set();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle message receival
        /// </summary>
        /// <param name="args"></param>
        private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (args?.ApplicationMessage == null || _isDisposed)
            {
                return;
            }

            _logger.LogTrace("Client {ClientId} received message on {Topic}", args.ClientId,
                args.ApplicationMessage.Topic);

            // Handle rpc outside of topic handling
            if (await HandleRpcAsync(args.ApplicationMessage, args.ProcessingFailed,
                (int)args.ReasonCode).ConfigureAwait(false))
            {
                return;
            }

            if (args.ProcessingFailed)
            {
                _logger.LogWarning("Failed to process MQTT message: {ReasonCode}",
                    args.ReasonCode);
                return;
            }

            // TODO: Add a wrapper interface over the list
            var properties = new Dictionary<string, string?>();
            if (args.ApplicationMessage.UserProperties != null)
            {
                foreach (var property in args.ApplicationMessage.UserProperties)
                {
                    properties.AddOrUpdate(property.Name, property.Value);
                }
            }

            await _subscriptionsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var topic = args.ApplicationMessage.Topic;
                foreach (var subscription in _subscriptions)
                {
                    if (TopicFilter.Matches(topic, subscription.Key))
                    {
                        foreach (var consumer in subscription.Value
                            .Where(s => s != IEventConsumer.Null))
                        {
                            await consumer.HandleAsync(topic,
                                new ReadOnlySequence<byte>(args.ApplicationMessage.PayloadSegment),
                                args.ApplicationMessage.ContentType ?? "NoContentType_UseMqttv5",
                                properties, this).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                _subscriptionsLock.Release();
            }
        }

        /// <summary>
        /// Handle disconnected
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();

            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} disconnected while {State} due to {Reason} ({ReasonString})",
                    _options.Value.ClientId, args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason, args.ReasonString ?? "unspecified");
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} disconnected while {State} due to {Reason} ({ReasonString})",
                    _options.Value.ClientId, args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason, args.ReasonString ?? "unspecified");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscription sync failed
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientSynchronizingFailed(ManagedProcessFailedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();

            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} failed to synchronize subscriptions",
                    _options.Value.ClientId);
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} failed to synchronize subscriptions",
                    _options.Value.ClientId);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connection state change
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientConnectionStateChanged(EventArgs args)
        {
            _logger.LogDebug("Client {ClientId} connection state change.",
                _options.Value.ClientId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Message published
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleMessagePublished(ApplicationMessageProcessedEventArgs args)
        {
            _inflight.TryRemove(args.ApplicationMessage.ApplicationMessage, out var tcs);
            if (args.Exception != null)
            {
                // publish failed, but it will be retried later...
                _logger.LogDebug(args.Exception,
                    "Client {ClientId} attempted but failed to publish message to {Topic}...",
                    _options.Value.ClientId, args.ApplicationMessage.ApplicationMessage.Topic);
                tcs?.TrySetException(args.Exception);
            }
            else
            {
                _logger.LogTrace("Client {ClientId} successfully published message to {Topic}.",
                    _options.Value.ClientId, args.ApplicationMessage.ApplicationMessage.Topic);
                tcs?.TrySetResult();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Message lost
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleMessageSkipped(ApplicationMessageSkippedEventArgs args)
        {
            _logger.LogDebug("Client {ClientId} dropped message for {Topic}.",
                _options.Value.ClientId, args.ApplicationMessage.ApplicationMessage.Topic);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connecting failed
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientConnectingFailed(ConnectingFailedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();
            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} failed to connect due to {Reason} ({ReasonString})",
                    _options.Value.ClientId, args.ConnectResult?.ResultCode ?? 0,
                    args.ConnectResult?.ReasonString ?? "Canceled");
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} failed to connect due to {Reason} ({ReasonString})",
                    _options.Value.ClientId, args.ConnectResult?.ResultCode ?? 0,
                    args.ConnectResult?.ReasonString ?? "Canceled");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create all subscriptions as soon as possible
        /// </summary>
        /// <returns></returns>
        private async Task SubscribeAsync(CancellationToken ct)
        {
            await _connection.ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                await _triggerSubscriber.WaitAsync(ct).ConfigureAwait(false);

                _triggerSubscriber.Reset();
                while (_topics.TryPeek(out var topic))
                {
                    await _subscriptionsLock.WaitAsync(ct).ConfigureAwait(false);
                    var clientIsConnected = false;
                    try
                    {
                        if (_subscriptions.ContainsKey(topic.Item2.Topic))
                        {
                            var client = GetClientForTopic(topic.Item2.Topic);
                            clientIsConnected = client.IsConnected;

                            // Subscribe right away
                            await client.InternalClient.SubscribeAsync(topic.Item2,
                                ct).ConfigureAwait(false);

                            _logger.LogDebug("Client {ClientId} subscribed to {Topic}",
                                _options.Value.ClientId, topic.Item2.Topic);

                            // Add as managed subscribtion
                            await client.SubscribeAsync(
                                new List<MqttTopicFilter> { topic.Item2 }).ConfigureAwait(false);
                        }
                        topic.Item1.TrySetResult();
                        _topics.TryDequeue(out _);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to subscribe on connect.");
                        if (clientIsConnected)
                        {
                            topic.Item1.TrySetException(ex);
                            _topics.TryDequeue(out _);
                        }
                    }
                    finally
                    {
                        _subscriptionsLock.Release();
                    }
                }
            }
        }

        /// <summary>
        /// Remove subscription and unsubscribe if needed
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="consumer"></param>
        /// <exception cref="ResourceInvalidStateException"></exception>
        private async ValueTask UnsubscribeAsync(string topic, IEventConsumer consumer)
        {
            await _subscriptionsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(topic, out var consumers))
                {
                    throw new ResourceInvalidStateException("Topic subscription not found");
                }
                consumers.Remove(consumer);
                if (consumers.Count == 0)
                {
                    await GetClientForTopic(topic).UnsubscribeAsync(topic).ConfigureAwait(false);
                    _subscriptions.Remove(topic);
                }
            }
            finally
            {
                _subscriptionsLock.Release();
            }
        }

        /// <summary>
        /// Get publisher client for topic waiting for connection
        /// </summary>
        private async ValueTask<IManagedMqttClient> GetClientAsync(string topic)
        {
            // Wait until connected
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_connection.IsCompleted)
            {
                await _connection.ConfigureAwait(false);
            }
            return GetClientForTopic(topic);
        }

        /// <summary>
        /// Get client for topic
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private IManagedMqttClient GetClientForTopic(string topic)
        {
            if (_clients.Length == 1)
            {
                return _clients[0];
            }
            System.Diagnostics.Debug.Assert(_clients.Length > 0);

            // Use a string hash for the bucket for now. Could be better...
            var topicHash = (uint)topic.GetHashCode(StringComparison.OrdinalIgnoreCase);
            return _clients[topicHash % _clients.Length];
        }

        /// <summary>
        /// Create client options
        /// </summary>
        /// <param name="partitionIndex"></param>
        /// <returns></returns>
        private ManagedMqttClientOptions GetClientOptions(int partitionIndex)
        {
            var tlsOptions = new MqttClientTlsOptions
            {
                CertificateValidationHandler = context =>
                {
                    if (_options.Value.AllowUntrustedCertificates ?? false)
                    {
                        return true;
                    }
                    return context.SslPolicyErrors == SslPolicyErrors.None;
                },
                AllowUntrustedCertificates =
                    _options.Value.AllowUntrustedCertificates ?? false,
                UseTls = _options.Value.UseTls ?? true,
            };
            var clientId = Identity;
            if (partitionIndex != 0)
            {
                clientId += "_" + partitionIndex.ToString(CultureInfo.InvariantCulture);
            }
            var options = new MqttClientOptions
            {
                ProtocolVersion = _options.Value.Protocol == MqttVersion.v5 ?
                        MqttProtocolVersion.V500 : MqttProtocolVersion.V311,
                ClientId = clientId,
                ThrowOnNonSuccessfulConnectResponse = false,
                CleanSession = false,
                Credentials = _options.Value.UserName != null ? new MqttClientCredentials(
                    _options.Value.UserName, _options.Value.Password == null ? null :
                        Encoding.UTF8.GetBytes(_options.Value.Password)) : null,
                ChannelOptions = _options.Value.WebSocketPath != null ?
                    new MqttClientWebSocketOptions
                    {
                        TlsOptions = tlsOptions,
                        Uri = new UriBuilder
                        {
                            Path = _options.Value.WebSocketPath,
                            Host = _options.Value.HostName,
                            Scheme = _options.Value.UseTls == true ? "https" : "http",
                            Port = _options.Value.Port ?? 0
                        }.ToString()
                    } :
                    new MqttClientTcpOptions
                    {
                        Server = _options.Value.HostName,
                        Port = _options.Value.Port,
                        TlsOptions = tlsOptions
                    },
                MaximumPacketSize = _options.Value.Protocol == MqttVersion.v5 ?
                    268435455u : 0u,
                KeepAlivePeriod = _options.Value.KeepAlivePeriod ?? TimeSpan.FromSeconds(15),
            };
            _options.Value.ConfigureMqttClient?.Invoke(options);
            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(options)
                .WithMaxPendingMessages(int.MaxValue)
                .WithAutoReconnectDelay(_options.Value.ReconnectDelay ?? TimeSpan.FromSeconds(5))
                .WithPendingMessagesOverflowStrategy(
                    MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                .Build();

            _options.Value.Configure?.Invoke(managedOptions);
            return managedOptions;
        }

        private readonly IOptions<MqttOptions> _options;
        private readonly ILogger _logger;
        private readonly IManagedMqttClient[] _clients;
        private readonly Task _connection;
        private readonly Task _subscriber;
        private readonly AsyncManualResetEvent _triggerSubscriber = new();
        private readonly ConcurrentQueue<(TaskCompletionSource, MqttTopicFilter)> _topics = new();
        private readonly ConcurrentDictionary<MqttApplicationMessage, TaskCompletionSource> _inflight = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _subscriptionsLock = new(1, 1);
        private readonly Dictionary<string, List<IEventConsumer>> _subscriptions = new();
        private bool _isDisposed;
    }
}
