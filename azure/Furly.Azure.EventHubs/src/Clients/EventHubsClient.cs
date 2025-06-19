// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Clients
{
    using Furly.Azure;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Producer;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IoT Hub cloud to device event client
    /// </summary>
    public sealed class EventHubsClient : IEventClient, IDisposable,
        IAsyncDisposable
    {
        /// <inheritdoc/>
        public string Name => "EventHub";

        /// <inheritdoc/>
        public string Identity { get; }

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes
            => _options.Value.MaxEventPayloadSizeInBytes ?? 1024 * 1024;

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        /// <param name="registry"></param>
        public EventHubsClient(IOptions<EventHubsClientOptions> options,
            ICredentialProvider credential, ILogger<EventHubsClient> logger,
            ISchemaRegistry? registry = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _schemaRegistry = registry;

            if (string.IsNullOrEmpty(_options.Value.ConnectionString) ||
                !ConnectionString.TryParse(_options.Value.ConnectionString, out var cs) ||
                string.IsNullOrEmpty(cs.Endpoint))
            {
                throw new InvalidConfigurationException(
                    "EventHub Connection string not configured.");
            }

            Identity = cs.Endpoint; // TODO

            _client = new EventHubProducerClient(_options.Value.ConnectionString);

            if (_schemaRegistry == null && options.Value.SchemaRegistry != null)
            {
                options.Value.SchemaRegistry.FullyQualifiedNamespace =
                    cs.Endpoint.Replace("sb://", string.Empty, StringComparison.Ordinal);

                _schemaRegistry = new SchemaGroup(options.Value.SchemaRegistry,
                    credential, _logger);
            }
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return new EventHubsEvent(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        internal sealed class EventHubsEvent : IEvent
        {
            /// <summary>
            /// Create event
            /// </summary>
            /// <param name="outer"></param>
            public EventHubsEvent(EventHubsClient outer)
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
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _contentEncoding = value;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
            {
                if (schema.Type == ContentMimeType.AvroSchema)
                {
                    _schema = schema;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                _properties.AddOrUpdate(name, value);
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
                _properties.Add("deviceId", value);
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
            public IEvent SetTimestamp(DateTimeOffset value)
            {
                return this;
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct)
            {
                try
                {
                    // Register the schema if not registered
                    if (_outer._schemaRegistry != null && _schema != null)
                    {
                        var retrievedSchemaId = await _outer._schemaRegistry.RegisterAsync(
                            _schema, ct).ConfigureAwait(false);

                        if (retrievedSchemaId != null)
                        {
                            _contentEncoding = $"{_contentEncoding}+{retrievedSchemaId}";
                        }
                    }

                    var eventBatch = await Client.CreateBatchAsync(ct).ConfigureAwait(false);
                    foreach (var msg in _buffers)
                    {
                        eventBatch.TryAdd(CreateMessage(msg));
                    }
                    await Client.SendAsync(eventBatch, ct).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.SendingMessageFailed(e);
                    throw; // e.Translate();
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _buffers.Clear();
            }

            /// <summary>
            /// Build message
            /// </summary>
            private EventData CreateMessage(ReadOnlySequence<byte> buffer)
            {
                var message = !buffer.IsSingleSegment ?
                    new EventData(buffer.ToArray()) :
                    new EventData(buffer.First);
                message.ContentType = _contentEncoding;
                foreach (var item in _properties)
                {
                    message.Properties.AddOrUpdate(item.Key, item.Value);
                }
                return message;
            }

            private ILogger Logger => _outer._logger;
            private EventHubProducerClient Client => _outer._client;

            private readonly EventHubsClient _outer;
            private readonly Dictionary<string, string?> _properties = [];
            private readonly List<ReadOnlySequence<byte>> _buffers = [];
            private IEventSchema? _schema;
            private string? _contentEncoding;
        }

        private readonly EventHubProducerClient _client;
        private readonly IOptions<EventHubsClientOptions> _options;
        private readonly ISchemaRegistry? _schemaRegistry;
        private readonly ILogger _logger;
    }

    /// <summary>
    /// Source-generated logging for EventHubsClient
    /// </summary>
    internal static partial class EventHubsClientLogging
    {
        [LoggerMessage(EventId = 0, Level = LogLevel.Trace,
            Message = "Sending message to to EventHub failed.")]
        public static partial void SendingMessageFailed(this ILogger logger, Exception ex);
    }
}
