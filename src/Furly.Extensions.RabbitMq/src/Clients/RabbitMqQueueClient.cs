// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.Messaging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// RabbitMq queue client
    /// </summary>
    public sealed class RabbitMqQueueClient : IEventClient, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "RabbitMqQueue";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => _connection.MaxMessageSizeInBytes;

        /// <inheritdoc/>
        public string Identity { get; }

        /// <summary>
        /// Create queue client
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="options"></param>
        public RabbitMqQueueClient(IRabbitMqConnection connection,
            IOptionsSnapshot<RabbitMqQueueOptions> options)
        {
            _connection = connection ??
                throw new ArgumentNullException(nameof(connection));
            Identity = options?.Value.Queue ?? string.Empty;
            _sendQueue = new Lazy<Task<IRabbitMqChannel>>(
                () => _connection.GetQueueChannelAsync(Identity));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_sendQueue.IsValueCreated)
            {
                _sendQueue.Value.Result.Dispose();
            }
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return _sendQueue.Value.Result.CreateEvent();
        }

        private readonly IRabbitMqConnection _connection;
        private readonly Lazy<Task<IRabbitMqChannel>> _sendQueue;
    }
}
