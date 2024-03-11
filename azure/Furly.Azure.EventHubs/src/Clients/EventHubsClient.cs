// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Clients
{
    using Furly.Azure;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using global::Azure.Data.SchemaRegistry;
    using global::Azure.Identity;
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
        /// <param name="logger"></param>
        public EventHubsClient(IOptions<EventHubsClientOptions> options,
            ILogger<EventHubsClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrEmpty(_options.Value.ConnectionString) ||
                !ConnectionString.TryParse(_options.Value.ConnectionString, out var cs) ||
                string.IsNullOrEmpty(cs.Endpoint))
            {
                throw new InvalidConfigurationException(
                    "EventHub Connection string not configured.");
            }

            Identity = _options.Value.SchemaGroupName ?? cs.Endpoint; // TODO
            _client = new EventHubProducerClient(_options.Value.ConnectionString);

            // Endpoint is sb://mschiertest11.servicebus.windows.net
            // Registry endpoint is mschiertest11.servicebus.windows.net
            _schemaRegistry = new SchemaRegistryClient(
                cs.Endpoint.Replace("sb://", string.Empty, StringComparison.Ordinal),
                    new DefaultAzureCredential());
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

        /// <summary>
        /// Publish schema to registry
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="schemaName"></param>
        /// <param name="version"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async ValueTask<string?> GetSchemaIdAsync(string schema,
            string schemaName, ulong version, CancellationToken ct)
        {
            // Check the cache
            if (_schemaToIdMap.TryGet(schemaName + version, out var value))
            {
                return value;
            }

            // TODO: versioning
            var schemaProperties = await _schemaRegistry.RegisterSchemaAsync(
                _options.Value.SchemaGroupName, schemaName, schema, SchemaFormat.Avro,
                ct).ConfigureAwait(false);

            var id = schemaProperties.Value.Id ?? string.Empty;
            _schemaToIdMap.AddOrUpdate(schemaName + version, id, schema.Length);
            return id;
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
            public IEvent SetTimestamp(DateTime value)
            {
                return this;
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct)
            {
                try
                {
                    // Register the schema if not registered
                    if (_outer._options.Value.SchemaGroupName != null &&
                        _schema != null)
                    {
                        var retrievedSchemaId = await _outer.GetSchemaIdAsync(
                            _schema.Schema, _schema.Name,
                            _schema.Version, ct).ConfigureAwait(false);

                        if (retrievedSchemaId != null)
                        {
                            _contentEncoding = $"{_contentEncoding}+{retrievedSchemaId}";
                        }
                    }

                    var eventBatch = await _client.CreateBatchAsync(ct).ConfigureAwait(false);
                    foreach (var msg in _buffers)
                    {
                        eventBatch.TryAdd(CreateMessage(msg));
                    }
                    await _client.SendAsync(eventBatch, ct).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogTrace(e, "Sending message to to EventHub failed.");
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

            private ILogger _logger => _outer._logger;
            private EventHubProducerClient _client => _outer._client;

            private readonly EventHubsClient _outer;
            private readonly Dictionary<string, string?> _properties = new();
            private readonly List<ReadOnlySequence<byte>> _buffers = new();
            private IEventSchema? _schema;
            private string? _contentEncoding;
        }

        private const int CacheCapacity = 128;
        private readonly LruCache<string, string> _schemaToIdMap = new(CacheCapacity);
        private readonly EventHubProducerClient _client;
        private readonly SchemaRegistryClient _schemaRegistry;
        private readonly IOptions<EventHubsClientOptions> _options;
        private readonly ILogger _logger;
    }
}
