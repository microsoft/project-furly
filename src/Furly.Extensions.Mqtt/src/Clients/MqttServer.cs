// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Serializers;
    using Furly.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MQTTnet;
    using MQTTnet.Protocol;
    using MQTTnet.Server;
    using MqttNetServer = MQTTnet.Server.MqttServer;
    using Nito.Disposables;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Mqtt broker that can serve as event client
    /// </summary>
    public sealed class MqttServer : MqttRpcBase, IEventClient, IEventSubscriber,
        IMqttPublish, IAwaitable<MqttServer>, IDisposable
    {
        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => MaxMethodPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity => _options.Value.HostName ?? string.Empty;

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public MqttServer(IOptions<MqttOptions> options, ISerializer serializer,
            ILogger<MqttServer> logger)
            : base(options, serializer, logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _server = CreateAsync();
        }

        /// <inheritdoc/>
        public IAwaiter<MqttServer> GetAwaiter()
        {
            return _server.ContinueWith(_ => this, scheduler: TaskScheduler.Default).AsAwaiter();
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
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
            var server = await GetServerAsync(ct).ConfigureAwait(false);
            await server.InjectApplicationMessage(
                new InjectedMqttApplicationMessage(message)
                {
                    SenderClientId = _options.Value.ClientId
                }, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
        {
            if (!TopicFilter.IsValid(topic))
            {
                throw new ArgumentException("Invalid topic filter", nameof(topic));
            }
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(topic, out var consumers))
                {
                    consumers = new List<IEventConsumer> { consumer };
                    var server = await GetServerAsync(ct).ConfigureAwait(false);
                    _subscriptions.Add(topic, consumers);
                }
                else
                {
                    consumers.Add(consumer);
                }
                return new AsyncDisposable(() => UnsubscribeAsync(topic, consumer));
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            // Dispose base server
            try
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop rpc server.");
            }
            try
            {
                if (!_server.IsFaulted && !_server.IsCanceled)
                {
                    _server.Result.Dispose();
                }
                _logger.LogDebug("Mqtt server disposed.");
            }
            finally
            {
                _lock.Dispose();
            }
        }

        /// <summary>
        /// Handle connection
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientConnectedAsync(ClientConnectedEventArgs args)
        {
            _logger.LogInformation("Client {ClientId} connected.", args.ClientId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle unsubscribe
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientUnsubscribedTopicAsync(ClientUnsubscribedTopicEventArgs args)
        {
            _logger.LogInformation("Client {ClientId} unsubscribed from {Topic}.",
                args.ClientId, args.TopicFilter);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle subscribe
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs args)
        {
            _logger.LogInformation("Client {ClientId} subscribed to {Topic}.",
                args.ClientId, args.TopicFilter);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle message receival
        /// </summary>
        /// <param name="args"></param>
        private async Task HandleMessageReceivedAsync(InterceptingPublishEventArgs args)
        {
            if (args?.ApplicationMessage == null)
            {
                return;
            }
            var topic = args.ApplicationMessage.Topic;
            _logger.LogTrace("Client received message from {Client} on {Topic}",
                args.ClientId, topic);

            // Handle rpc outside of topic handling
            if (await HandleRpcAsync(args.ApplicationMessage, false, 0).ConfigureAwait(false))
            {
                args.ProcessPublish = false;
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

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var subscription in _subscriptions)
                {
                    if (TopicFilter.Matches(topic, subscription.Key))
                    {
                        foreach (var consumer in subscription.Value)
                        {
                            await consumer.HandleAsync(topic,
                                new ReadOnlySequence<byte>(args.ApplicationMessage.PayloadSegment),
                                args.ApplicationMessage.ContentType ?? "NoContentType_UseMqttv5",
                                properties, this).ConfigureAwait(false);
                        }
                        args.ProcessPublish = false;
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Handle connection
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task ValidateConnectionAsync(ValidatingConnectionEventArgs args)
        {
            if (_options.Value.UserName != null &&
                args.UserName != _options.Value.UserName)
            {
                args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            }
            else if (_options.Value.Password != null &&
                args.Password != _options.Value.Password)
            {
                args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            }
            else
            {
                args.ReasonCode = MqttConnectReasonCode.Success;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle disconnected
        /// </summary>
        /// <param name="args"></param>
        private Task HandleClientDisconnectedAsync(ClientDisconnectedEventArgs args)
        {
            _logger.LogInformation("Disconnected client {ClientId} with type {Reason}",
                args.ClientId, args.DisconnectType);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Remove subscription and unsubscribe if needed
        /// </summary>
        /// <param name="target"></param>
        /// <param name="consumer"></param>
        /// <exception cref="ResourceInvalidStateException"></exception>
        private async ValueTask UnsubscribeAsync(string target, IEventConsumer consumer)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(target, out var consumers))
                {
                    throw new ResourceInvalidStateException("Subscription not found");
                }
                consumers.Remove(consumer);
                if (consumers.Count == 0)
                {
                    _subscriptions.Remove(target);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Get server
        /// </summary>
        private async Task<MqttNetServer> GetServerAsync(CancellationToken ct)
        {
            return await _server.WaitAsync(ct).ConfigureAwait(false);
        }

        /// <summary>ping
        /// Create client and start it
        /// </summary>
        public async Task<MqttNetServer> CreateAsync()
        {
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                ;
            if (_options.Value.UseTls == true)
            {
                optionsBuilder = optionsBuilder
                    .WithoutDefaultEndpoint()
                    .WithEncryptedEndpoint()
                    .WithEncryptionSslProtocol(SslProtocols.None)
                    .WithEncryptedEndpointPort(_options.Value.Port ?? 8883);
            }
            else
            {
                optionsBuilder = optionsBuilder
                    .WithDefaultEndpointPort(_options.Value.Port ?? 1883);
            }

            var options = optionsBuilder.Build();
            options.KeepAliveOptions.MonitorInterval = _options.Value.KeepAlivePeriod ??
                TimeSpan.FromMilliseconds(500);
            var server = new MqttFactory().CreateMqttServer(options);
            try
            {
                if (_options.Value.UserName != null || _options.Value.Password != null)
                {
                    server.ValidatingConnectionAsync += ValidateConnectionAsync;
                }

                server.ClientConnectedAsync += HandleClientConnectedAsync;
                server.ClientDisconnectedAsync += HandleClientDisconnectedAsync;
                server.ClientSubscribedTopicAsync += HandleClientSubscribedTopicAsync;
                server.ClientUnsubscribedTopicAsync += HandleClientUnsubscribedTopicAsync;
                server.InterceptingPublishAsync += HandleMessageReceivedAsync;

                await server.StartAsync().ConfigureAwait(false);
                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        private readonly IOptions<MqttOptions> _options;
        private readonly ILogger _logger;
        private readonly Task<MqttNetServer> _server;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly Dictionary<string, List<IEventConsumer>> _subscriptions = new();
        private bool _isDisposed;
    }
}
