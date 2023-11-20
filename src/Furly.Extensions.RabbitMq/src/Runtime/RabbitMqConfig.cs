// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// RabbitMq configuration
    /// </summary>
    internal sealed class RabbitMqConfig : PostConfigureOptionBase<RabbitMqOptions>
    {
        /// <inheritdoc/>
        public RabbitMqConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, RabbitMqOptions options)
        {
            if (string.IsNullOrEmpty(options.HostName))
            {
                options.HostName =
                    GetStringOrDefault(EnvironmentVariable.RABBITMQHOSTNAME,
                    GetStringOrDefault("_RABBITMQ_HOST", "localhost"));
            }
            if (string.IsNullOrEmpty(options.UserName))
            {
                options.UserName =
                    GetStringOrDefault(EnvironmentVariable.RABBITMQUSERNAME, "user");
            }
            if (string.IsNullOrEmpty(options.Key))
            {
                options.Key =
                    GetStringOrDefault(EnvironmentVariable.RABBITMQKEY, "bitnami");
            }
        }
    }
}
