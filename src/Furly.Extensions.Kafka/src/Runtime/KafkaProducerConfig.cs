// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Kafka producer configuration
    /// </summary>
    internal sealed class KafkaProducerConfig : PostConfigureOptionBase<KafkaProducerOptions>
    {
        /// <inheritdoc/>
        public KafkaProducerConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, KafkaProducerOptions options)
        {
            if (string.IsNullOrEmpty(options.Topic))
            {
                options.Topic = "furly";
            }
        }
    }
}
