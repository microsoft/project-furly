// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// Kafka consumer configuration
    /// </summary>
    internal sealed class KafkaConsumerConfig : PostConfigureOptionBase<KafkaConsumerOptions>
    {
        /// <inheritdoc/>
        public KafkaConsumerConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, KafkaConsumerOptions options)
        {
            if (options.CheckpointInterval == null)
            {
                options.CheckpointInterval = TimeSpan.FromMinutes(1);
            }
#if DEBUG
            if (options.SkipEventsOlderThan == null)
            {
                options.SkipEventsOlderThan = TimeSpan.FromMinutes(5);
            }
#endif
            if (string.IsNullOrEmpty(options.ConsumerTopic))
            {
                options.ConsumerTopic =
                    GetStringOrDefault(EnvironmentVariable.KAFKACONSUMERTOPICREGEX);
            }
            if (string.IsNullOrEmpty(options.ConsumerGroup))
            {
                options.ConsumerGroup =
                    GetStringOrDefault(EnvironmentVariable.KAFKACONSUMERGROUP, "$default");
            }
        }
    }
}
