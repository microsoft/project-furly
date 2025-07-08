// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Metrics;
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Mqtt.Clients;
    using Furly.Extensions.Serializers;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Protocol.Events;
    using global::Azure.Iot.Operations.Protocol.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio sdk Pub sub client adapter
    /// </summary>
    internal sealed class AioMqttClient : IMqttPubSubClient, IEventClient, IAwaitable<IMqttPubSubClient>
    {
        /// <inheritdoc/>
        public string? ClientId => _client.ClientId;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion => (MqttProtocolVersion)_client.ProtocolVersion;

        /// <inheritdoc/>
        public string Name => "Aio";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _client.MaxEventPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity => _client.Identity;

        /// <inheritdoc/>
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <summary>
        /// Create aio mqtt client
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="meter"></param>
        public AioMqttClient(ApplicationContext context, IOptions<AioOptions> options,
            ILoggerFactory logger, ISerializer serializer, IMeterProvider? meter = null)
        {
            _context = context;
            _logger = logger.CreateLogger<AioMqttClient>();
            var mqttClientOptions = options.Value.Mqtt;
            if (string.IsNullOrEmpty(mqttClientOptions.ClientId))
            {
                throw new ArgumentException("Client id is null or empty");
            }
            mqttClientOptions = mqttClientOptions with
            {
                ClientId = mqttClientOptions.ClientId + "Rpc", // Set client id
                NumberOfClientPartitions = 1
            };
            _client = new MqttClient(Options.Create(mqttClientOptions), logger.CreateLogger<MqttClient>(),
                serializer, meter);
            _client.MessageReceived = OnReceiveAsync;
            _logger.Connecting(mqttClientOptions.ClientId);
        }

        /// <inheritdoc/>
        public IEvent CreateEvent() => new AioMqttMessage(this, _client.CreateEvent());

        /// <inheritdoc/>
        public IAwaiter<IMqttPubSubClient> GetAwaiter()
        {
            return AwaitAsync(_client, _logger).AsAwaiter(this);
            static async Task AwaitAsync(MqttClient client, ILogger _logger)
            {
                await client;
                _logger.Connected(client.ClientId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync(bool disposing)
        {
            _client.MessageReceived = null;
            var clientId = _client.ClientId;
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.Disposed(clientId);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => DisposeAsync(disposing: true);

        /// <inheritdoc/>
        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage,
            CancellationToken cancellationToken = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var buffer = applicationMessage.Payload.ToArray();
                var payload = MQTTnet.MqttApplicationMessageExtensions
                    .ConvertPayloadToString(applicationMessage.FromSdkType());
                _logger.Send(ClientId, payload);
            }
            return _client.PublishAsync(applicationMessage.FromSdkType(), cancellationToken)
                .ContinueWith(t => t.Result.ToSdkType(), cancellationToken,
                    TaskContinuationOptions.None, TaskScheduler.Current);
        }

        /// <inheritdoc/>
        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options,
            CancellationToken cancellationToken = default)
        {
            return _client.SubscribeAsync(options.FromSdkType(), cancellationToken)
                .ContinueWith(t => t.Result.ToSdkType(), cancellationToken,
                    TaskContinuationOptions.None, TaskScheduler.Current);
        }

        /// <inheritdoc/>
        public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options,
            CancellationToken cancellationToken = default)
        {
            return _client.UnsubscribeAsync(options.FromSdkType(), cancellationToken)
                .ContinueWith(t => t.Result.ToSdkType(), cancellationToken,
                    TaskContinuationOptions.None, TaskScheduler.Current);
        }

        private Task OnReceiveAsync(MqttMessageReceivedEventArgs args)
        {
            if (ApplicationMessageReceivedAsync == null)
            {
                return Task.CompletedTask;
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var buffer = args.ApplicationMessage.Payload.ToArray();
                var payload = MQTTnet.MqttApplicationMessageExtensions
                    .ConvertPayloadToString(args.ApplicationMessage);
                _logger.Receive(args.ClientId, payload);
            }
            return ApplicationMessageReceivedAsync.Invoke(
                args.ToSdkType((a, ct) => args.AcknowledgeAsync(ct)));
        }

        /// <summary>
        /// Message wrapper
        /// </summary>
        private sealed class AioMqttMessage : IEvent
        {
            /// <inheritdoc/>
            public AioMqttMessage(AioMqttClient client, IEvent ev)
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

            private readonly AioMqttClient _client;
            private readonly IEvent _event;
        }

        private readonly MqttClient _client;
        private readonly ILogger _logger;
        private readonly ApplicationContext _context;
    }

    /// <summary>
    /// Source-generated logging for AioMqttClient
    /// </summary>
    internal static partial class AioMqttClientLogging
    {
        private const int EventClass = 30;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Connecting client {ClientId} ...")]
        public static partial void Connecting(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Client connected with client id {ClientId}.")]
        public static partial void Connected(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Client with client id {ClientId} was disposed.")]
        public static partial void Disposed(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Debug, SkipEnabledCheck = true,
            Message = "Client id {ClientId} sent: {Payload}.")]
        public static partial void Send(this ILogger logger, string? clientId, string? payload);

        [LoggerMessage(EventId = EventClass + 4, Level = LogLevel.Debug, SkipEnabledCheck = true,
            Message = "Client id {ClientId} received: {Payload}.")]
        public static partial void Receive(this ILogger logger, string? clientId, string? payload);
    }
}
