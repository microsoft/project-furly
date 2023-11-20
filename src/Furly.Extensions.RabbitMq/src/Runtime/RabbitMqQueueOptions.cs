// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    /// <summary>
    /// Queue consumer configuration
    /// </summary>
    public class RabbitMqQueueOptions
    {
        /// <summary>
        /// Queue to consume from
        /// </summary>
        public string? Queue { get; set; }
    }
}
