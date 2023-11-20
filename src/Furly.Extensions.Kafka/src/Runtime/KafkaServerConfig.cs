// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Kafka configuration
    /// </summary>
    internal sealed class KafkaServerConfig : PostConfigureOptionBase<KafkaServerOptions>
    {
        /// <inheritdoc/>
        public KafkaServerConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, KafkaServerOptions options)
        {
            if (string.IsNullOrEmpty(options.BootstrapServers))
            {
                options.BootstrapServers = GetStringOrDefault(
                    EnvironmentVariable.KAFKABOOTSTRAPSERVERS, "localhost:9092");
            }
            if (options.Partitions == 0)
            {
                options.Partitions =
                    GetIntOrDefault(EnvironmentVariable.KAFKAPARTITIONCOUNT, 8);
            }
            if (options.ReplicaFactor == 0)
            {
                options.ReplicaFactor =
                    GetIntOrDefault(EnvironmentVariable.KAFKAREPLICAFACTOR, 2);
            }
        }
    }
}
