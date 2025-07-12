// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Metrics;
    using Furly.Extensions.Serializers;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.StateStore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio mqtt publisher
    /// </summary>
    internal sealed class AioDssPublisher : IEventClient, IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "AioDss";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _client.InnerClient.MaxEventPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity => _client.InnerClient.Identity;

        /// <summary>
        /// Create aio mqtt publisher
        /// </summary>
        /// <param name="context"></param>
        /// <param name="sdk"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="registry"></param>
        /// <param name="meter"></param>
        public AioDssPublisher(ApplicationContext context, IAioSdk sdk,
            IOptions<AioOptions> options, ILoggerFactory logger,
            ISerializer serializer, IAioSrClient? registry = null,
            IMeterProvider? meter = null)
        {
            _context = context;
            _registry = registry;
            _logger = logger.CreateLogger<AioMqttClient>();
            var mqttClientOptions = options.Value.Mqtt;
            if (string.IsNullOrEmpty(mqttClientOptions.ClientId))
            {
                throw new ArgumentException("Publisher client id is null or empty");
            }
            mqttClientOptions = mqttClientOptions with
            {
                NumberOfClientPartitions = 1
            };
            _client = new AioMqttClient(context, options, logger, serializer, meter,
                "dss");
            _dss = sdk.CreateStateStoreClient(_client);
            _logger.DssPublisherConnecting(mqttClientOptions.ClientId);
        }

        /// <inheritdoc/>
        public IEvent CreateEvent() => new AioDssMessage(this);

        /// <inheritdoc/>
        public IAwaiter<IEventClient> GetAwaiter()
        {
            return AwaitAsync(_client, _logger).AsAwaiter(this);
            static async Task AwaitAsync(AioMqttClient client, ILogger _logger)
            {
                await client;
                _logger.DssPublisherConnected(client.ClientId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var clientId = _client.ClientId;
            await _dss.DisposeAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.DssPublisherDisposed(clientId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Message wrapper
        /// </summary>
        private sealed class AioDssMessage : IEvent
        {
            /// <inheritdoc/>
            public AioDssMessage(AioDssPublisher client)
            {
                _client = client;
            }

            /// <inheritdoc/>
            public IEvent AsCloudEvent(CloudEventHeader header) => this;

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value) => this;

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTimeOffset value)
            {
                _ts = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                _contentType = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value) => this;

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
            {
                _schema = schema;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                _key = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value) => this;

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                _expiryTime = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
            {
                _buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value) => this;

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct = default)
            {
                string? schemaId = null;
                if (_client._registry != null && _schema != null)
                {
                    schemaId = await _client._registry.RegisterAsync(_schema,
                        ct).ConfigureAwait(false);
                }

                var key = new StateStoreKey(_key ?? "default");
                await _client._context.ApplicationHlc.UpdateNowAsync(
                    cancellationToken: ct).ConfigureAwait(false);
                if (_buffers.Count == 0)
                {
                    var deleteOptions = new StateStoreDeleteRequestOptions
                    {
                        FencingToken = _client._context.ApplicationHlc
                    };
                    await _client._dss.DeleteAsync(key, deleteOptions,
                        cancellationToken: ct).ConfigureAwait(false);
                    return;
                }
                var options = new StateStoreSetRequestOptions
                {
                    FencingToken = _client._context.ApplicationHlc,
                    ExpiryTime = _expiryTime > TimeSpan.Zero ? _expiryTime : null,
                };
                if (_buffers.Count == 1)
                {
                    var value = new StateStoreValue(_buffers[0].ToArray());
                    await _client._dss.SetAsync(key, value, options,
                        cancellationToken: ct).ConfigureAwait(false);
                }
                else
                {
                    // Concatenate all buffers into a single byte array
                    var totalLength = _buffers.Sum(b => b.Length);
                    var combinedBuffer = new byte[totalLength];
                    var offset = 0;
                    foreach (var buffer in _buffers)
                    {
                        buffer.CopyTo(combinedBuffer.AsSpan(offset));
                        offset = checked(offset + checked((int)buffer.Length));
                    }
                    var value = new StateStoreValue(combinedBuffer);
                    await _client._dss.SetAsync(key, value, options,
                        cancellationToken: ct).ConfigureAwait(false);
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            private DateTimeOffset _ts;
            private string? _contentType;
            private string? _key;
            private TimeSpan _expiryTime;
            private IEventSchema? _schema;
            private readonly List<ReadOnlySequence<byte>> _buffers = [];
            private readonly AioDssPublisher _client;
        }

        private readonly AioMqttClient _client;
        private readonly IStateStoreClient _dss;
        private readonly ILogger _logger;
        private readonly ApplicationContext _context;
        private readonly IAioSrClient? _registry;
    }

    /// <summary>
    /// Source-generated logging for AioDssPublisher
    /// </summary>
    internal static partial class AioDssPublisherLogging
    {
        private const int EventClass = 30;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Publisher client {ClientId} connecting ...")]
        public static partial void DssPublisherConnecting(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Publisher client connected with client id {ClientId}.")]
        public static partial void DssPublisherConnected(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Publisher client {ClientId} disposed.")]
        public static partial void DssPublisherDisposed(this ILogger logger, string? clientId);
    }
}
