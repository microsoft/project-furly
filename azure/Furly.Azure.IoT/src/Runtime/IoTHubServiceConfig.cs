// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// IoT hub services runtime configuration
    /// </summary>
    public sealed class IoTHubServiceConfig : PostConfigureOptionBase<IoTHubServiceOptions>
    {
        /// <inheritdoc/>
        public IoTHubServiceConfig(IConfiguration configuration) :
            base(configuration)
        {
        }
        /// <inheritdoc/>
        public override void PostConfigure(string? name, IoTHubServiceOptions options)
        {
            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                options.ConnectionString = GetStringOrDefault(
                    EnvironmentVariables.PCS_IOTHUB_CONNSTRING,
                        GetStringOrDefault("_HUB_CS", string.Empty));
            }
        }
    }
}
