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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio sdk Pub sub client adapter
    /// </summary>
    internal sealed class AioMqttClient : IMqttPubSubClient, IAwaitable<IMqttPubSubClient>, IDisposable
    {
        /// <inheritdoc/>
        public string? ClientId => _client.ClientId;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion => (MqttProtocolVersion)_client.ProtocolVersion;

        /// <inheritdoc/>
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <summary>
        /// Acces to the inner client
        /// </summary>
        internal IEventClient InnerClient => _client;

        /// <summary>
        /// Create aio mqtt client
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="meter"></param>
        /// <param name="postFix"></param>
        public AioMqttClient(ApplicationContext context, IOptions<AioOptions> options,
            ILoggerFactory logger, ISerializer serializer, IMeterProvider? meter = null,
            string? postFix = null)
        {
            _context = context;
            _logger = logger.CreateLogger<AioMqttClient>();
            var mqttClientOptions = options.Value.Mqtt;
            if (string.IsNullOrEmpty(mqttClientOptions.ClientId))
            {
                throw new ArgumentException("Sdk client id is null or empty");
            }
            mqttClientOptions = mqttClientOptions with
            {
                ClientId = mqttClientOptions.ClientId + "_" + (postFix ?? "sdk"),
                NumberOfClientPartitions = 1
            };
            _client = new MqttClient(Options.Create(mqttClientOptions),
                logger.CreateLogger<MqttClient>(), serializer, null, meter);
            _client.MessageReceived = OnReceiveAsync;
            _logger.SdkConnecting(mqttClientOptions.ClientId);
        }

        /// <inheritdoc/>
        public IAwaiter<IMqttPubSubClient> GetAwaiter()
        {
            return AwaitAsync(_client, _logger).AsAwaiter(this);
            static async Task AwaitAsync(MqttClient client, ILogger _logger)
            {
                await client;
                _logger.SdkConnected(client.ClientId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync(bool disposing)
        {
            _client.MessageReceived = null;
            var clientId = _client.ClientId;
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.SdkDisposed(clientId);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => DisposeAsync(disposing: true);

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage,
            CancellationToken cancellationToken = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var buffer = applicationMessage.Payload.ToArray();
                var payload = MQTTnet.MqttApplicationMessageExtensions
                    .ConvertPayloadToString(applicationMessage.FromSdkType());
                _logger.SdkSend(ClientId, payload);
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
                _logger.SdkReceive(args.ClientId, payload);
            }
            return ApplicationMessageReceivedAsync.Invoke(
                args.ToSdkType((a, ct) => args.AcknowledgeAsync(ct)));
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
        private const int EventClass = 50;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Sdk mqtt client {ClientId} connecting...")]
        public static partial void SdkConnecting(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Sdk mqtt client {ClientId} connected.")]
        public static partial void SdkConnected(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Sdk mqtt client {ClientId} disposed.")]
        public static partial void SdkDisposed(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Debug, SkipEnabledCheck = true,
            Message = "Sdk mqtt client {ClientId} sent: {Payload}.")]
        public static partial void SdkSend(this ILogger logger, string? clientId, string? payload);

        [LoggerMessage(EventId = EventClass + 4, Level = LogLevel.Debug, SkipEnabledCheck = true,
            Message = "Sdk mqtt client {ClientId} received: {Payload}.")]
        public static partial void SdkReceive(this ILogger logger, string? clientId, string? payload);
    }
}
