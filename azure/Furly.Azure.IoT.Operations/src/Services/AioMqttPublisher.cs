// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Metrics;
    using Furly.Extensions.Mqtt.Clients;
    using Furly.Extensions.Serializers;
    using global::Azure.Iot.Operations.Protocol;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio mqtt publisher
    /// </summary>
    internal sealed class AioMqttPublisher : IEventClient, IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "AioMqtt";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _client.MaxEventPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity => _client.Identity;

        /// <summary>
        /// Create aio mqtt publisher
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="registry"></param>
        /// <param name="meter"></param>
        public AioMqttPublisher(ApplicationContext context, IOptions<AioOptions> options,
            ILoggerFactory logger, ISerializer serializer, IAioSrClient? registry = null,
            IMeterProvider? meter = null)
        {
            _context = context;
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
            _client = new MqttClient(Options.Create(mqttClientOptions),
                logger.CreateLogger<MqttClient>(), serializer, registry, meter);
            _logger.MqttPublisherConnecting(mqttClientOptions.ClientId);
        }

        /// <inheritdoc/>
        public IEvent CreateEvent() => new AioMqttMessage(this, _client.CreateEvent());

        /// <inheritdoc/>
        public IAwaiter<IEventClient> GetAwaiter()
        {
            return AwaitAsync(_client, _logger).AsAwaiter(this);
            static async Task AwaitAsync(MqttClient client, ILogger _logger)
            {
                await client;
                _logger.MqttPublisherConnected(client.ClientId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync(bool disposing)
        {
            _client.MessageReceived = null;
            var clientId = _client.ClientId;
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.MqttPublisherDisposed(clientId);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => DisposeAsync(disposing: true);

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Message wrapper
        /// </summary>
        private sealed class AioMqttMessage : IEvent
        {
            /// <inheritdoc/>
            public AioMqttMessage(AioMqttPublisher client, IEvent ev)
            {
                _client = client;
                _event = ev;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
                => _event.SetTopic(value);

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTimeOffset value)
                => _event.SetTimestamp(value);

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
                => _event.SetContentType(value);

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
                => _event.SetContentEncoding(value);

            /// <inheritdoc/>
            public IEvent AsCloudEvent(CloudEventHeader header)
                => _event.AsCloudEvent(header);

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
                => _event.SetSchema(schema);

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
                => _event.AddProperty(name, value);

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
                => _event.SetRetain(value);

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
                => _event.SetQoS(value);

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
                => _event.SetTtl(value);

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
                => _event.AddBuffers(value);

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct = default)
            {
                // add hlc
                await _client._context.ApplicationHlc.UpdateNowAsync(
                    cancellationToken: ct).ConfigureAwait(false);
                _event.AddProperty(AkriSystemProperties.Timestamp,
                    _client._context.ApplicationHlc.EncodeToString());

                _event.AddProperty("__protVer", "1.0");
                _event.AddProperty("__srcId", _client.Identity);

                await _event.SendAsync(ct).ConfigureAwait(false);
            }

            /// <inheritdoc/>
            public void Dispose() => _event.Dispose();

            private readonly AioMqttPublisher _client;
            private readonly IEvent _event;
        }

        private readonly MqttClient _client;
        private readonly ILogger _logger;
        private readonly ApplicationContext _context;
    }

    /// <summary>
    /// Source-generated logging for AioPublisher
    /// </summary>
    internal static partial class AioMqttPublisherLogging
    {
        private const int EventClass = 60;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Publisher client {ClientId} connecting ...")]
        public static partial void MqttPublisherConnecting(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Publisher client connected with client id {ClientId}.")]
        public static partial void MqttPublisherConnected(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Publisher client {ClientId} disposed.")]
        public static partial void MqttPublisherDisposed(this ILogger logger, string? clientId);
    }
}
