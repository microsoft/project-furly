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
    using System.Net;
    using MQTTnet.Exceptions;
    using Furly.Extensions.Serializers;

    /// <summary>
    /// Mqtt event client
    /// </summary>
    public sealed class MqttClient : MqttRpcBase, IEventClient, IEventSubscriber,
        IMqttPublish, IDisposable, IAwaitable<MqttClient>
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
        /// <param name="serializer"></param>
        /// <param name="registry"></param>
        public MqttClient(IOptions<MqttOptions> options, ILogger<MqttClient> logger,
            ISerializer serializer, ISchemaRegistry? registry = null)
            : base(options, serializer, logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Identity = _options.Value.ClientId ?? Guid.NewGuid().ToString();
            var numberofPartitions = _options.Value.NumberOfClientPartitions ?? 0;
            _clients = Enumerable
                .Range(0, numberofPartitions == 0 ? 1 : numberofPartitions)
                .Select(id =>
                {
                    var client = new ManagedMqttClient($"Identity_{id}");

                    client.Client.ConnectedAsync +=
                        args => HandleClientConnectedAsync(client, args);
                    client.Client.ConnectingFailedAsync +=
                        args => HandleClientConnectingFailed(client, args);
                    client.Client.ConnectionStateChangedAsync +=
                        args => HandleClientConnectionStateChanged(client, args);
                    client.Client.SynchronizingSubscriptionsFailedAsync +=
                        args => HandleClientSynchronizingFailed(client, args);
                    client.Client.DisconnectedAsync +=
                        args => HandleClientDisconnectedAsync(client, args);
                    client.Client.ApplicationMessageProcessedAsync +=
                        args => HandleMessagePublished(client, args);
                    client.Client.ApplicationMessageSkippedAsync +=
                        args => HandleMessageSkipped(client, args);
                    client.Client.ApplicationMessageReceivedAsync +=
                        args => HandleMessageReceivedAsync(client, args);

                    return client;
                })
                .ToArray();

            _cts = new CancellationTokenSource();
            _publisher = _options.Value.ConfigureSchemaMessage == null && registry == null ? this
                : new MqttSchemaPublisher(_options, this, registry);
            _connection = Task.WhenAll(_clients
                .Select((c, i) => c.StartAsync(_logger, GetClientOptions(i)))).WaitAsync(_cts.Token);
            _subscriber = Task.Factory.StartNew(() => SubscribeAsync(_cts.Token),
                _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <inheritdoc/>
        public IAwaiter<MqttClient> GetAwaiter()
        {
            return _connection.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            _logger.LogDebug("Closing {ClientId} ...", _options.Value.ClientId);

            // Dispose base server
            try
            {
                await base.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop rpc server.");
            }

            // Stop subscriber
            try
            {
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
                    .Select(c => c.StopAsync(_logger))).ConfigureAwait(false);
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
                _isDisposed = true;
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
            ObjectDisposedException.ThrowIf(_isDisposed, _publisher);
            return new MqttMessage(_options, this);
        }

        /// <inheritdoc/>
        protected override ValueTask<IAsyncDisposable> SubscribeAsync(string topicFilter,
            CancellationToken ct)
        {
            return SubscribeAsync(topicFilter, IEventConsumer.Null, ct);
        }

        /// <inheritdoc/>
        public override async ValueTask PublishAsync(MqttApplicationMessage message,
            IEventSchema? schema, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();
            var client = await GetClientAsync(message.Topic, ct).ConfigureAwait(false);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _inflight.TryAdd(message, tcs);
            try
            {
                await client.Client.EnqueueAsync(message).WaitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _inflight.TryRemove(message, out _);
                throw;
            }
            await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle connection
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        private Task HandleClientConnectedAsync(ManagedMqttClient client,
            MqttClientConnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _logger.LogInformation("Client connected with {Result} as {ClientId}.",
                args.ConnectResult.ResultCode, client.Id);
            _triggerSubscriber.Set();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle message receival
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        private async Task HandleMessageReceivedAsync(ManagedMqttClient client,
            MqttApplicationMessageReceivedEventArgs args)
        {
            if (args?.ApplicationMessage == null || _isDisposed)
            {
                return;
            }

            _logger.LogTrace("Client {ClientId} received message on {Topic}", client.Id,
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
        /// <param name="client"></param>
        /// <param name="args"></param>
        private Task HandleClientDisconnectedAsync(ManagedMqttClient client,
            MqttClientDisconnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();

            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} disconnected while {State} due to {Reason} ({ReasonString})",
                    client.Id, args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason, args.ReasonString ?? "unspecified");
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} disconnected while {State} due to {Reason} ({ReasonString})",
                    client.Id, args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason, args.ReasonString ?? "unspecified");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscription sync failed
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientSynchronizingFailed(ManagedMqttClient client,
            ManagedProcessFailedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();

            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} failed to synchronize subscriptions", client.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} failed to synchronize subscriptions", client.Id);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connection state change
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientConnectionStateChanged(ManagedMqttClient client,
            EventArgs args)
        {
            if (client.Client.IsConnected)
            {
                _logger.LogDebug("Client {ClientId} connected.", client.Id);
                client.Connected.Set();
            }
            else
            {
                _logger.LogDebug("Client {ClientId} disconnected.", client.Id);
                client.Connected.Reset();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Message published
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleMessagePublished(ManagedMqttClient client,
            ApplicationMessageProcessedEventArgs args)
        {
            _inflight.TryRemove(args.ApplicationMessage.ApplicationMessage, out var tcs);
            if (args.Exception != null)
            {
                // publish failed, but it will be retried later...
                _logger.LogDebug(args.Exception,
                    "Client {ClientId} attempted but failed to publish message to {Topic}...",
                    client.Id, args.ApplicationMessage.ApplicationMessage.Topic);
                tcs?.TrySetException(args.Exception);
            }
            else
            {
                _logger.LogTrace("Client {ClientId} successfully published message to {Topic}.",
                    client.Id, args.ApplicationMessage.ApplicationMessage.Topic);
                tcs?.TrySetResult();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Message lost
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleMessageSkipped(ManagedMqttClient client,
            ApplicationMessageSkippedEventArgs args)
        {
            _logger.LogDebug("Client {ClientId} dropped message for {Topic}.",
                client.Id, args.ApplicationMessage.ApplicationMessage.Topic);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connecting failed
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task HandleClientConnectingFailed(ManagedMqttClient client,
            ConnectingFailedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();
            client.Connected.Reset();
            if (args.Exception != null)
            {
                _logger.LogError(args.Exception,
                    "Client {ClientId} failed to connect due to {Reason} ({ReasonString})",
                    client.Id, args.ConnectResult?.ResultCode ?? 0,
                    args.ConnectResult?.ReasonString ?? "Canceled");
            }
            else
            {
                _logger.LogWarning(
                    "Client {ClientId} failed to connect due to {Reason} ({ReasonString})",
                    client.Id, args.ConnectResult?.ResultCode ?? 0,
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
            await _connection.WaitAsync(ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                await _triggerSubscriber.WaitAsync(ct).ConfigureAwait(false);

                _triggerSubscriber.Reset();
                while (_topics.TryPeek(out var topic))
                {
                    // We try to subscribe here even if one client is not connected.
                    await _subscriptionsLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (_subscriptions.ContainsKey(topic.Item2.Topic))
                        {
                            var client = GetClientForTopic(topic.Item2.Topic);
                            await client.Connected.WaitAsync(ct).ConfigureAwait(false);

                            // Subscribe right away
                            await client.Client.InternalClient.SubscribeAsync(topic.Item2,
                                ct).ConfigureAwait(false);

                            _logger.LogDebug("Client {ClientId} subscribed to {Topic}",
                                client.Id, topic.Item2.Topic);

                            // Add as managed subscription
                            await client.Client.SubscribeAsync(
                                new List<MqttTopicFilter> { topic.Item2 }).ConfigureAwait(false);
                        }
                        topic.Item1.TrySetResult();
                        _topics.TryDequeue(out _);
                    }
                    catch (MqttClientNotConnectedException nce)
                    {
                        _logger.LogDebug(nce, "Failed to subscribe on connect. Retrying...");
                        // Rety
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to subscribe.");
                        topic.Item1.TrySetException(ex);
                        _topics.TryDequeue(out _);
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
                    await GetClientForTopic(topic).Client.UnsubscribeAsync(topic).ConfigureAwait(false);
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
        private async ValueTask<ManagedMqttClient> GetClientAsync(string topic, CancellationToken ct)
        {
            // Wait until connected
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_connection.IsCompleted)
            {
                await _connection.WaitAsync(ct).ConfigureAwait(false);
            }
            return GetClientForTopic(topic);
        }

        /// <summary>
        /// Get client for topic
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private ManagedMqttClient GetClientForTopic(string topic)
        {
            if (_clients.Length == 1)
            {
                return _clients[0];
            }
            System.Diagnostics.Debug.Assert(_clients.Length > 0);

            // Use a string hash for the bucket for now. Could be better...
            var topicHash = (uint)topic.GetHashCode(StringComparison.Ordinal);
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
                            Host = _options.Value.HostName ?? "localhost",
                            Scheme = _options.Value.UseTls == true ? "https" : "http",
                            Port = _options.Value.Port ?? 0
                        }.ToString()
                    } :
                    new MqttClientTcpOptions
                    {
                        RemoteEndpoint = new DnsEndPoint(
                            _options.Value.HostName ?? "localhost", _options.Value.Port ?? 1883),
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

        /// <summary>
        /// Schema registry publisher
        /// </summary>
        internal sealed class MqttSchemaPublisher : IMqttPublish
        {
            /// <summary>
            /// Create schema registry implementation
            /// </summary>
            /// <param name="options"></param>
            /// <param name="publish"></param>
            /// <param name="registry"></param>
            public MqttSchemaPublisher(IOptions<MqttOptions> options, IMqttPublish publish,
                ISchemaRegistry? registry)
            {
                _version = options.Value.Protocol;
                _configure = options.Value.ConfigureSchemaMessage;
                _publish = publish;
                _registry = registry;
            }

            /// <summary>
            /// Publish
            /// </summary>
            /// <param name="message"></param>
            /// <param name="schema"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public async ValueTask PublishAsync(MqttApplicationMessage message,
                IEventSchema? schema, CancellationToken ct)
            {
                if (schema != null)
                {
                    string? id;
                    if (_registry == null)
                    {
                        //
                        // TODO: Hardcode a subpath and retain for now.
                        //
                        var builder = new MqttApplicationMessageBuilder()
                            .WithPayload(schema.Schema)
                            .WithTopic(message.Topic + "/schema")
                            .WithRetainFlag(true)
                            .WithContentType(schema.Type)
                            ;
                        var schemaMessage = builder.Build();
                        _configure?.Invoke(schemaMessage);
                        await _publish.PublishAsync(schemaMessage, null,
                            ct).ConfigureAwait(false);
                        id = schema.Id;
                    }
                    else
                    {
                        id = await _registry.RegisterAsync(schema, ct).ConfigureAwait(false);
                    }
                    if (id != null && _version != MqttVersion.v311)
                    {
                        // Add the schema id as cloud event property
                        message.UserProperties.Add(
                            new MqttUserProperty("dataschema", id));
                    }
                }
                await _publish.PublishAsync(message, schema, ct).ConfigureAwait(false);
            }

            private readonly MqttVersion _version;
            private readonly Action<MqttApplicationMessage>? _configure;
            private readonly IMqttPublish _publish;
            private readonly ISchemaRegistry? _registry;
        }

        /// <summary>
        /// Client wrapper
        /// </summary>
        /// <param name="Id"></param>
        private sealed record class ManagedMqttClient(string Id) : IDisposable
        {
            /// <summary>
            /// Connect event
            /// </summary>
            public AsyncManualResetEvent Connected { get; } = new(false);

            /// <summary>
            /// Inner client
            /// </summary>
            public IManagedMqttClient Client { get; } = new MqttFactory().CreateManagedMqttClient();

            /// <summary>
            /// Start
            /// </summary>
            /// <param name="logger"></param>
            /// <param name="options"></param>
            /// <returns></returns>
            public async Task StartAsync(ILogger logger, ManagedMqttClientOptions options)
            {
                await Client.StartAsync(options).ConfigureAwait(false);
                logger.LogDebug("Started client id {ClientId}.", Id);
            }

            /// <summary>
            /// Stop
            /// </summary>
            /// <param name="logger"></param>
            /// <param name="cleanDisconnect"></param>
            /// <returns></returns>
            public async Task StopAsync(ILogger logger, bool cleanDisconnect = true)
            {
                await Client.StopAsync(cleanDisconnect).ConfigureAwait(false);
                logger.LogDebug("Client id {ClientId} closed successfully.", Id);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                Client.Dispose();
            }
        }

        private readonly IOptions<MqttOptions> _options;
        private readonly ILogger _logger;
        private readonly ManagedMqttClient[] _clients;
        private readonly Task _connection;
        private readonly Task _subscriber;
        private readonly AsyncManualResetEvent _triggerSubscriber = new();
        private readonly ConcurrentQueue<(TaskCompletionSource, MqttTopicFilter)> _topics = new();
        private readonly ConcurrentDictionary<MqttApplicationMessage, TaskCompletionSource> _inflight = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly IMqttPublish _publisher;
        private readonly SemaphoreSlim _subscriptionsLock = new(1, 1);
        private readonly Dictionary<string, List<IEventConsumer>> _subscriptions = new();
        private bool _isDisposed;
    }
}
