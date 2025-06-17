// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Extensions.Configuration;
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
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio sdk Pub sub client adapter
    /// </summary>
    internal sealed class AioMqttClient : IMqttPubSubClient, IAwaitable<IMqttPubSubClient>
    {
        /// <inheritdoc/>
        public string? ClientId => _client.ClientId;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion => (MqttProtocolVersion)_client.ProtocolVersion;

        /// <inheritdoc/>
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <summary>
        /// Create aio mqtt client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="meter"></param>
        public AioMqttClient(IOptions<MqttOptions> options, ILoggerFactory logger, ISerializer serializer,
            IMeterProvider? meter = null)
        {
            _logger = logger.CreateLogger<AioMqttClient>();
            var mqttClientOptions = options.Value;
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
            _logger.LogInformation("Connecting client {ClientId} ...", mqttClientOptions.ClientId);
        }

        /// <inheritdoc/>
        public IAwaiter<IMqttPubSubClient> GetAwaiter()
        {
            return AwaitAsync(_client, _logger).AsAwaiter(this);
            static async Task AwaitAsync(MqttClient client, ILogger _logger)
            {
                await client;
                _logger.LogInformation("Client connected with client id {ClientId}.", client.ClientId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync(bool disposing)
        {
            _client.MessageReceived = null;
            var clientId = _client.ClientId;
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Client with client id {ClientId} was disposed.", clientId);

        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => DisposeAsync(disposing: true);

        /// <inheritdoc/>
        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage,
            CancellationToken cancellationToken = default)
        {
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
            return ApplicationMessageReceivedAsync.Invoke(
                args.ToSdkType((a, ct) => args.AcknowledgeAsync(ct)));
        }

        private readonly MqttClient _client;
        private readonly ILogger _logger;
    }
}
