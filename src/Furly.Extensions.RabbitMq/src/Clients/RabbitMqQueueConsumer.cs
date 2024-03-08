// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.Messaging;
    using Autofac;
    using Microsoft.Extensions.Options;
    using RabbitMQ.Client;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// RabbitMq queue consumer
    /// </summary>
    public sealed class RabbitMqQueueConsumer : IEventSubscriber, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "RabbitMqQueue";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _connection.MaxMessageSizeInBytes;

        /// <inheritdoc/>
        public string Identity { get; }

        /// <summary>
        /// Create queue client
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="options"></param>
        public RabbitMqQueueConsumer(IRabbitMqConnection connection,
            IOptionsSnapshot<RabbitMqQueueOptions> options)
        {
            _connection = connection ??
                throw new ArgumentNullException(nameof(connection));
            Identity = options?.Value.Queue ?? string.Empty;
            _recvQueue = new Lazy<Task<QueueConsumer>>(
                () => QueueConsumer.CreateAsync(this));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_recvQueue.IsValueCreated)
            {
                _recvQueue.Value.Result.Dispose();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct = default)
        {
            var rcvQueue = await _recvQueue.Value.ConfigureAwait(false);
            return rcvQueue.Subscribe(topic, consumer);
        }

        /// <summary>
        /// Queue consumer
        /// </summary>
        private sealed class QueueConsumer : IRabbitMqConsumer, IDisposable
        {
            private QueueConsumer(RabbitMqQueueConsumer outer)
            {
                _queue = outer._connection.GetQueueChannelAsync(outer.Identity, this);
            }

            /// <summary>
            /// Create consumer
            /// </summary>
            /// <param name="outer"></param>
            /// <returns></returns>
            internal static async Task<QueueConsumer> CreateAsync(RabbitMqQueueConsumer outer)
            {
                var consumer = new QueueConsumer(outer);
                await consumer._queue.ConfigureAwait(false);
                return consumer;
            }

            /// <inheritdoc/>
            public async Task HandleBasicDeliver(IModel model, ulong deliveryTag,
                bool redelivered, string exchange, string routingKey,
                IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                if (!properties.IsTypePresent() || !properties.IsContentTypePresent())
                {
                    return;
                }

                // TODO: Add a wrapper interface over the properties
                var userProperties = new Dictionary<string, string?>()
                {
                    ["ContentEncoding"] = properties.ContentEncoding
                };

                IEnumerable<Task> handles;
                lock (_subscriptions)
                {
                    handles = _subscriptions
                        .Where(subscription => subscription.Matches(properties.Type))
                        .Select(subscription => subscription.Consumer.HandleAsync(
                            properties.Type, new ReadOnlySequence<byte>(body),
                            properties.ContentType, userProperties, null));
                }
                await Task.WhenAll(handles).ConfigureAwait(false);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _queue.Dispose();
            }

            /// <summary>
            /// Subscribe consumer
            /// </summary>
            /// <param name="topic"></param>
            /// <param name="consumer"></param>
            /// <returns></returns>
            public IAsyncDisposable Subscribe(string topic, IEventConsumer consumer)
            {
                var subscription = new Subscription(topic, consumer, this);
                lock (_subscriptions)
                {
                    _subscriptions.Add(subscription);
                }
                return subscription;
            }

            private sealed class Subscription : IAsyncDisposable
            {
                /// <summary>
                /// Consumer
                /// </summary>
                public IEventConsumer Consumer { get; }

                /// <summary>
                /// Registered target
                /// </summary>
                public string Filter { get; }

                public Subscription(string topicFilter, IEventConsumer consumer,
                    QueueConsumer outer)
                {
                    _outer = outer;
                    Consumer = consumer;
                    Filter = topicFilter;
                }

                /// <summary>
                /// Returns true if the target matches the subscription.
                /// </summary>
                /// <param name="topic"></param>
                /// <returns></returns>
                public bool Matches(string topic)
                {
                    return TopicFilter.Matches(topic, Filter);
                }

                /// <inheritdoc/>
                public ValueTask DisposeAsync()
                {
                    lock (_outer._subscriptions)
                    {
                        _outer._subscriptions.Remove(this);
                    }
                    return ValueTask.CompletedTask;
                }

                private readonly QueueConsumer _outer;
            }

            private readonly Task<IRabbitMqChannel> _queue;
            private readonly List<Subscription> _subscriptions = new();
        }

        private readonly IRabbitMqConnection _connection;
        private readonly Lazy<Task<QueueConsumer>> _recvQueue;
    }
}
