// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka
{
    /// <summary>
    /// Kafka configuration
    /// </summary>
    public class KafkaServerOptions
    {
        /// <summary>
        /// Comma seperated bootstrap servers
        /// </summary>
        public string? BootstrapServers { get; set; }

        /// <summary>
        /// Number of partitions per topic
        /// </summary>
        public int Partitions { get; set; }

        /// <summary>
        /// Replica factor of new topics
        /// </summary>
        public int ReplicaFactor { get; set; }
    }
}
