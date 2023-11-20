// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    using Furly.Extensions.Messaging;
    using System;

    /// <summary>
    /// Channel
    /// </summary>
    public interface IRabbitMqChannel : IDisposable
    {
        /// <summary>
        /// Queue
        /// </summary>
        string QueueName { get; }

        /// <summary>
        /// Exchange
        /// </summary>
        string ExchangeName { get; }

        /// <summary>
        /// Create event
        /// </summary>
        /// <param name="mandatory"></param>
        /// <returns></returns>
        IEvent CreateEvent(bool mandatory = false);
    }
}
