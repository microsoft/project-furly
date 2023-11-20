// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.Messaging;
    using Microsoft.Extensions.Logging;
    using Nito.Disposables;
    using RabbitMQ.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Event bus built on top of rabbitmq
    /// </summary>
    public sealed class RabbitMqBrokerClient : IEventClient, IEventSubscriber,
        IDisposable
    {
        /// <inheritdoc/>
        public string Name => "RabbitMqBroker";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _connection.MaxMessageSizeInBytes;

        /// <inheritdoc/>
        public string Identity => _connection.Name;

        /// <summary>
        /// Create topic client
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="logger"></param>
        public RabbitMqBrokerClient(IRabbitMqConnection connection,
            ILogger<RabbitMqBrokerClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _channel = new Lazy<Task<IRabbitMqChannel>>(
                () => _connection.GetTopicChannelAsync());
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return _channel.Value.Result.CreateEvent();
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
                var tag = Guid.NewGuid().ToString();
                if (!_subscriptions.TryGetValue(topic, out var handler))
                {
                    handler = await Subscription.CreateAsync(this, topic).ConfigureAwait(false);
                    _subscriptions.TryAdd(topic, handler);
                }
                handler.Add(tag, consumer);
                return new AsyncDisposable(() => DisposeAsync(topic, tag));
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_channel.IsValueCreated)
            {
                _channel.Value.Result.Dispose();
            }
            _lock.Dispose();
        }

        /// <summary>
        /// Dispose subscription
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="subscriptionId"></param>
        /// <returns></returns>
        private async ValueTask DisposeAsync(string topic, string subscriptionId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_subscriptions.TryGetValue(topic, out var subscription)
                    && subscription.Remove(subscriptionId, out _))
                {
                    // Clean up consumer
                    subscription.Dispose();
                    _subscriptions.Remove(topic);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Subscription holder
        /// </summary>
        private class Subscription : IRabbitMqConsumer, IDisposable
        {
            /// <summary>
            /// Subscribed to topic
            /// </summary>
            public string Topic { get; }

            /// <summary>
            /// Create consumer
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="topic"></param>
            private Subscription(RabbitMqBrokerClient outer, string topic)
            {
                _outer = outer;
                Topic = topic;
                // Register this consumer on pub/sub connection
                _channel = outer._connection.GetTopicChannelAsync(
                    ToRoutingKey(topic).Replace("+", "*", StringComparison.Ordinal), this);
            }

            /// <summary>
            /// Create subscription
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="topic"></param>
            /// <returns></returns>
            internal static async Task<Subscription> CreateAsync(
                RabbitMqBrokerClient outer, string topic)
            {
                var subscription = new Subscription(outer, topic);
                await subscription._channel.ConfigureAwait(false);
                return subscription;
            }

            /// <inheritdoc/>
            public bool Remove(string token, out IEventConsumer? handler)
            {
                _lock.Wait();
                try
                {
                    if (_consumers.TryGetValue(token, out var found))
                    {
                        _consumers.Remove(token);
                        handler = found;
                        if (_consumers.Count == 0)
                        {
                            return true; // Calls dispose
                        }
                    }
                    else
                    {
                        handler = null;
                    }
                    return false;
                }
                finally
                {
                    _lock.Release();
                }
            }

            /// <inheritdoc/>
            public void Add(string token, IEventConsumer handler)
            {
                _lock.Wait();
                try
                {
                    var first = _consumers.Count == 0;
                    _consumers.Add(token, handler);
                }
                finally
                {
                    _lock.Release();
                }
            }

            /// <inheritdoc/>
            public async Task HandleBasicDeliver(IModel model,
                ulong deliveryTag, bool redelivered, string exchange,
                string routingKey, IBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                List<IEventConsumer> consumers;
                try
                {
                    consumers = _consumers.Values.ToList();
                }
                finally
                {
                    _lock.Release();
                }
                var topic = ToTopic(routingKey);
                // TODO: Add a wrapper interface over the properties
                var userProperties = new Dictionary<string, string?>()
                {
                    ["ContentEncoding"] = properties.ContentEncoding
                };
                foreach (var handler in consumers)
                {
                    await handler.HandleAsync(topic, body, properties.ContentType,
                        userProperties, _outer).ConfigureAwait(false);
                }
                _outer._logger.LogTrace(
                    "<----- Received message on {Topic} with {ContentType}... ",
                    routingKey, properties.ContentType);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _channel.Result.Dispose();
                _lock.Dispose();
            }

            private readonly RabbitMqBrokerClient _outer;
            private readonly Task<IRabbitMqChannel> _channel;
            private readonly SemaphoreSlim _lock = new(1, 1);
            private readonly Dictionary<string, IEventConsumer> _consumers = new();
        }

        /// <summary>
        /// Convert to a topic name
        /// </summary>
        /// <param name="routingKey"></param>
        /// <returns></returns>
        private static string ToTopic(string routingKey)
        {
            return routingKey.Replace('.', '/');
        }

        /// <summary>
        /// Convert to routing key
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private static string ToRoutingKey(string topic)
        {
            return topic.Replace('/', '.');
        }

        private readonly Dictionary<string, Subscription> _subscriptions = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger _logger;
        private readonly IRabbitMqConnection _connection;
        private readonly Lazy<Task<IRabbitMqChannel>> _channel;
    }
}
