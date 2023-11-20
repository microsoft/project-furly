// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka
{
    /// <summary>
    /// Kafka producer configuration
    /// </summary>
    public class KafkaProducerOptions
    {
        /// <summary>
        /// Produce to topic (null = default)
        /// </summary>
        public string? Topic { get; set; }

        /// <summary>
        /// The value configured in message.max.bytes
        /// (Default: 1 MB)
        /// </summary>
        public int? MessageMaxBytes { get; set; }
    }
}
