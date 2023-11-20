// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Runtime
{
    using Furly.Extensions.RabbitMq;
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// RabbitMq configuration
    /// </summary>
    internal sealed class RabbitMqQueueConfig : PostConfigureOptionBase<RabbitMqQueueOptions>
    {
        /// <inheritdoc/>
        public RabbitMqQueueConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, RabbitMqQueueOptions options)
        {
            if (string.IsNullOrEmpty(options.Queue))
            {
                options.Queue = GetStringOrDefault("RABBITMQ_QUEUE", "default");
            }
        }
    }
}
