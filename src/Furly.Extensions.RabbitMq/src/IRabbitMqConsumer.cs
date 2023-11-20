// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    using RabbitMQ.Client;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Consumer
    /// </summary>
    public interface IRabbitMqConsumer
    {
        /// <summary>
        /// Handle delivery
        /// </summary>
        /// <param name="model"></param>
        /// <param name="deliveryTag"></param>
        /// <param name="redelivered"></param>
        /// <param name="exchange"></param>
        /// <param name="routingKey"></param>
        /// <param name="properties"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        Task HandleBasicDeliver(IModel model,
            ulong deliveryTag, bool redelivered, string exchange,
            string routingKey, IBasicProperties properties,
            ReadOnlyMemory<byte> body);
    }
}
