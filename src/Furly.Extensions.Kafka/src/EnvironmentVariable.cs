// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka
{
    /// <summary>
    /// Runtime environment variables
    /// </summary>
    public static class EnvironmentVariable
    {
        /// <summary> Kafka Boostrap servers </summary>
        public const string KAFKABOOTSTRAPSERVERS =
            "KAFKA_BOOTSTRAP_SERVERS";
        /// <summary> Kafka partitions per topic </summary>
        public const string KAFKAPARTITIONCOUNT =
            "KAFKA_PARTITION_COUNT";
        /// <summary> Kafka replica factor per topic </summary>
        public const string KAFKAREPLICAFACTOR =
            "KAFKA_REPLICA_FACTOR";
        /// <summary> Kafka Consumer group </summary>
        public const string KAFKACONSUMERGROUP =
            "KAFKA_CONSUMER_GROUP";
        /// <summary> Kafka Consumer topics </summary>
        public const string KAFKACONSUMERTOPICREGEX =
            "KAFKA_CONSUMER_TOPIC_REGEX";
    }
}
