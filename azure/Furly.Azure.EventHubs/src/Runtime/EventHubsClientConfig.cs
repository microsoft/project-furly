// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// IoT hub services runtime configuration
    /// </summary>
    public sealed class EventHubsClientConfig : PostConfigureOptionBase<EventHubsClientOptions>
    {
        /// <inheritdoc/>
        public EventHubsClientConfig(IConfiguration configuration) :
            base(configuration)
        {
        }
        /// <inheritdoc/>
        public override void PostConfigure(string? name, EventHubsClientOptions options)
        {
            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                options.ConnectionString = GetStringOrDefault(
                    EnvironmentVariables.PCS_EVENTHUB_CONNECTIONSTRING,
                        GetStringOrDefault("_EH_CS", string.Empty));
            }
        }
    }
}
