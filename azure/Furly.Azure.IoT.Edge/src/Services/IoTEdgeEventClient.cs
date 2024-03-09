// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure.IoT.Edge;
    using Furly.Extensions.Messaging;
    using Microsoft.Azure.Devices.Client;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Event client implementation
    /// </summary>
    public sealed class IoTEdgeEventClient : IEventClient, IEventSubscriber,
        IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "IoTEdge";

        /// <inheritdoc/>
        public string Identity => _identity.AsString();

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes { get; } = 252 * 1024; // 256 KB - leave 4 kb for properties

        /// <summary>
        /// Create Event client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="identity"></param>
        public IoTEdgeEventClient(IIoTEdgeDeviceClient client, IIoTEdgeDeviceIdentity identity)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _receiver = new Lazy<Task>(
                () => _client.SetMessageHandlerAsync(OnReceivedAsync, null));
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_receiver.IsValueCreated)
            {
                await _client.SetMessageHandlerAsync(null, null).ConfigureAwait(false);
                _subscriptions.Clear();
            }
            System.Diagnostics.Debug.Assert(_subscriptions.Count == 0);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public IEvent CreateEvent()
        {
            return new IoTEdgeMessage(this);
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
        {
            if (!TopicFilter.IsValid(topic))
            {
                throw new ArgumentException("Invalid topic filter", nameof(topic));
            }
            var subscription = new Subscription(topic, consumer, this);
            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }
            // Ensure we are receiving messages
            await _receiver.Value.ConfigureAwait(false);
            return subscription;
        }

        /// <summary>
        /// Handle received message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private async Task<MessageResponse> OnReceivedAsync(Message message,
            object? userContext)
        {
            var target = message.InputName ?? message.MessageSchema ?? string.Empty;

            IEnumerable<Task> handles;
            lock (_subscriptions)
            {
                var payload = message.GetBytes();
                handles = _subscriptions
                    .Where(subscription => subscription.Matches(target))
                    .Select(subscription => subscription.Consumer.HandleAsync(target,
                        new ReadOnlySequence<byte>(payload), message.ContentType,
                        message.Properties.AsReadOnly(), this));
            }
            await Task.WhenAll(handles).ConfigureAwait(false);
            return handles
                .Select(h => h.IsFaulted ?
                    MessageResponse.None : MessageResponse.Completed)
                .FirstOrDefault(MessageResponse.Abandoned);
        }

        private sealed class IoTEdgeMessage : IEvent
        {
            /// <summary>
            /// Create message
            /// </summary>
            /// <param name="outer"></param>
            public IoTEdgeMessage(IoTEdgeEventClient outer)
            {
                _outer = outer;
            }

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _template.ContentType = value;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _template.ContentEncoding = value;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                _template.Properties.AddOrUpdate(name, value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
            {
                _buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                _topic = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTime value)
            {
                return this;
            }

            /// <inheritdoc />
            public async ValueTask SendAsync(CancellationToken ct)
            {
                var messages = AsMessages();
                try
                {
                    if (messages.Count == 1)
                    {
                        await _outer._client.SendEventAsync(messages[0],
                            _topic, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _outer._client.SendEventBatchAsync(messages,
                            _topic, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    foreach (var hubMessage in messages)
                    {
                        hubMessage.Dispose();
                    }
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _buffers.Clear();
                _template.Dispose();
            }

            /// <summary>
            /// Build message
            /// </summary>
            internal List<Message> AsMessages()
            {
                return _buffers.ConvertAll(m => _template.CloneWithBody(m.ToArray()));
            }

            private readonly List<ReadOnlySequence<byte>> _buffers = new();
            private readonly Message _template = new();
            private readonly IoTEdgeEventClient _outer;
            private string? _topic;
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

            /// <summary>
            /// Create subscription
            /// </summary>
            /// <param name="filter"></param>
            /// <param name="consumer"></param>
            /// <param name="outer"></param>
            public Subscription(string filter, IEventConsumer consumer,
                IoTEdgeEventClient outer)
            {
                Filter = filter;
                Consumer = consumer;
                _outer = outer;
            }

            /// <summary>
            /// Returns true if the topic matches the subscription.
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

            private readonly IoTEdgeEventClient _outer;
        }

        private readonly Lazy<Task> _receiver;
        private readonly IIoTEdgeDeviceClient _client;
        private readonly IIoTEdgeDeviceIdentity _identity;
        private readonly List<Subscription> _subscriptions = new();
    }
}
