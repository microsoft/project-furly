// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests
{
    using Furly.Extensions.Messaging;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class InMemoryEventBroker : IEventClient, IEventSubscriber
    {
        /// <inheritdoc/>
        public string Identity => string.Empty;

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes { get; set; } = 256 * 1024;

        /// <inheritdoc/>
        public string Name => "Test";

        /// <inheritdoc/>
        public ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct = default)
        {
            if (!TopicFilter.IsValid(topic))
            {
                throw new ArgumentException("Invalid topic filter", nameof(topic));
            }
            return ValueTask.FromResult(Add(topic, consumer));
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return new Event(this);
        }

        /// <summary>
        /// Simple add
        /// </summary>
        public IAsyncDisposable Add(string target, IEventConsumer consumer)
        {
            lock (_consumers)
            {
                _consumers.TryAdd(target, consumer);
            }
            return new Disposer(() =>
            {
                lock (_consumers)
                {
                    _consumers.Remove(target, out _);
                }
            });
        }

        /// <summary>
        /// Get matching subscription
        /// </summary>
        public IEnumerable<IEventConsumer> GetMatchingConsumers(string topic)
        {
            lock (_consumers)
            {
                return _consumers
                    .Where(subscription => TopicFilter.Matches(topic, subscription.Key))
                    .Select(subscription => subscription.Value)
                    .ToList();
            }
        }

        /// <summary>
        /// Broker event
        /// </summary>
        private sealed class Event : IEvent
        {
            public Event(InMemoryEventBroker outer)
            {
                _outer = outer;
            }

            /// <inheritdoc/>
            public QoS QoS { get; private set; }

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
            {
                QoS = value;
                return this;
            }

            /// <inheritdoc/>
            public DateTimeOffset Timestamp { get; private set; }

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTimeOffset value)
            {
                Timestamp = value;
                return this;
            }

            /// <inheritdoc/>
            public string? ContentType { get; private set; }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                ContentType = value;
                return this;
            }

            /// <inheritdoc/>
            public string? ContentEncoding { get; private set; }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                ContentEncoding = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
            {
                return this;
            }

            /// <inheritdoc/>
            public string? Topic { get; private set; }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                Topic = value;
                return this;
            }

            /// <inheritdoc/>
            public bool Retain { get; private set; }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
            {
                Retain = value;
                return this;
            }

            /// <inheritdoc/>
            public TimeSpan Ttl { get; private set; }

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                Ttl = value;
                return this;
            }

            /// <inheritdoc/>
            public List<ReadOnlySequence<byte>> Buffers { get; } = new();

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
            {
                Buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public Dictionary<string, string?> Properties { get; } = new();

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                Properties.AddOrUpdate(name, value);
                return this;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct = default)
            {
                var found = false;
                foreach (var consumer in _outer.GetMatchingConsumers(Topic ?? string.Empty))
                {
                    found = true;
                    foreach (var data in Buffers)
                    {
                        await consumer.HandleAsync(Topic ?? string.Empty, data,
                            ContentType ?? string.Empty, Properties, _outer, ct).ConfigureAwait(false);
                    }
                }
                if (!found)
                {
                    throw new ArgumentException($"Could not find subscription for {Topic}");
                }
            }

            private readonly InMemoryEventBroker _outer;
        }

        private sealed class Disposer : IAsyncDisposable
        {
            public Disposer(Action action)
            {
                _action = action;
            }
            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                _action.Invoke();
                return ValueTask.CompletedTask;
            }
            private readonly Action _action;
        }

        private readonly Dictionary<string, IEventConsumer> _consumers = new();
    }
}
