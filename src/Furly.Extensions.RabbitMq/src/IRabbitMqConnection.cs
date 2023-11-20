// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a connection
    /// </summary>
    public interface IRabbitMqConnection
    {
        /// <summary>
        /// The connection name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Max message size
        /// </summary>
        public int MaxMessageSizeInBytes { get; }

        /// <summary>
        /// Get a rabbit mq queue channel.
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="consumer"></param>
        /// <returns>The created channel</returns>
        Task<IRabbitMqChannel> GetQueueChannelAsync(string queue,
            IRabbitMqConsumer? consumer = null);

        /// <summary>
        /// Get a rabbit mq topic channel for the desired
        /// identity.
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="consumer"></param>
        /// <returns>The created channel</returns>
        Task<IRabbitMqChannel> GetTopicChannelAsync(string? topic = null,
            IRabbitMqConsumer? consumer = null);
    }
}
