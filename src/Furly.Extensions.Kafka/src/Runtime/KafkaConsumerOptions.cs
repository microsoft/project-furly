// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka
{
    using System;

    /// <summary>
    /// Kafka consumer configuration
    /// </summary>
    public class KafkaConsumerOptions
    {
        /// <summary>
        /// Consumer group id
        /// </summary>
        public string? ConsumerGroup { get; set; }

        /// <summary>
        /// Subscribe to topic (null = all)
        /// </summary>
        public string? ConsumerTopic { get; set; }

        /// <summary>
        /// Whether to read from end or start.
        /// </summary>
        public bool InitialReadFromEnd { get; set; }

        /// <summary>
        /// Set checkpoint interval. null = never.
        /// </summary>
        public TimeSpan? CheckpointInterval { get; set; }

        /// <summary>
        /// Skip all events older than. null = never.
        /// </summary>
        public TimeSpan? SkipEventsOlderThan { get; set; }
    }
}
