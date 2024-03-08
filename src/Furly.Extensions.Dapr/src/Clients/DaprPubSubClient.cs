// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Messaging;
    using global::Dapr.Client;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Event client built on top of dapr
    /// </summary>
    public sealed class DaprPubSubClient : IEventClient, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "Dapr";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes { get; }

        /// <inheritdoc/>
        public string Identity => Guid.NewGuid().ToString();

        /// <summary>
        /// Create dapr client
        /// </summary>
        /// <param name="options"></param>
        public DaprPubSubClient(IOptions<DaprOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _component = options.Value.PubSubComponent;
            _client = options.Value.CreateClient();

            MaxEventPayloadSizeInBytes =
                options.Value.MessageMaxBytes ?? 512 * 1024 * 1024;
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return new DaprPubSubEvent(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.Dispose();
        }

        /// <summary>
        /// Event wrapper
        /// </summary>
        private sealed class DaprPubSubEvent : IEvent
        {
            /// <summary>
            /// Create event
            /// </summary>
            /// <param name="outer"></param>
            public DaprPubSubEvent(DaprPubSubClient outer)
            {
                _outer = outer;
            }

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
            {
                AddProperty("qos", ((int)value).ToString(CultureInfo.InvariantCulture));
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                _topic = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTime value)
            {
                AddProperty("TimeStamp", value.ToString(CultureInfo.InvariantCulture));
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                _contentType = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                AddProperty("ContentEncoding", value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetSchema(string name, ulong version,
                ReadOnlyMemory<byte> schema, string contentType)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                if (value == null)
                {
                    _metadata.Remove(name);
                }
                else
                {
                    _metadata.AddOrUpdate(name, value);
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
            {
                AddProperty("retain", value ? "true" : "false");
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                AddProperty("ttlInSeconds",
                    value.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
            {
                _buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct)
            {
                if (_buffers.Count == 0)
                {
                    return;
                }

                var topic = _topic;
                if (topic == null)
                {
                    throw new InvalidOperationException("Need a valid topic.");
                }

                var pubSubName = _outer._component;
                if (string.IsNullOrEmpty(pubSubName))
                {
                    // Split the pub sub target system from the topic structure
                    var split = topic.IndexOf('/', StringComparison.Ordinal);
                    if (split == -1)
                    {
                        Throw();
                    }
                    pubSubName = topic[..split];
                    if (pubSubName.Length == 0)
                    {
                        Throw();
                    }

                    topic = topic[(split + 1)..];
                }

                foreach (var buffer in _buffers)
                {
                    await _outer._client.PublishByteEventAsync(pubSubName, topic,
                        buffer.IsSingleSegment ? buffer.First : buffer.ToArray(),
                        _contentType, _metadata, ct).ConfigureAwait(false);
                }

                static void Throw()
                {
                    throw new InvalidOperationException("Because no pub sub " +
                        "component was defined in the configuration options, " +
                        "the Topic must contain component name as first part " +
                        "of the path.");
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _buffers.Clear();
            }

            private string? _topic;
            private string? _contentType;
            private readonly Dictionary<string, string> _metadata = new();
            private readonly List<ReadOnlySequence<byte>> _buffers = new();
            private readonly DaprPubSubClient _outer;
        }

        private readonly string? _component;
        private readonly DaprClient _client;
    }
}
