// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Mqtt;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Protocol.Events;
    using global::Azure.Iot.Operations.Protocol.Models;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio sdk Pub sub client adapter
    /// </summary>
    public sealed class AioMqttClient : IMqttPubSubClient
    {
        /// <inheritdoc/>
        public string? ClientId => _client.ClientId;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion
            => (MqttProtocolVersion)_client.ProtocolVersion;

        /// <inheritdoc/>
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <summary>
        /// Create adapter
        /// </summary>
        /// <param name="client"></param>
        public AioMqttClient(IManagedClient client)
        {
            _client = client;
            _client.MessageReceived = OnReceiveAsync;
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync(bool disposing)
        {
            _client.MessageReceived = null;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _client.MessageReceived = null;
            return ValueTask.CompletedTask;
        }

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

        private readonly IManagedClient _client;
    }
}
