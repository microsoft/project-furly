// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// IoT hub device runtime configuration
    /// </summary>
    public sealed class IoTHubDeviceConfig : PostConfigureOptionBase<IoTHubDeviceOptions>
    {
        /// <inheritdoc/>
        public IoTHubDeviceConfig(IConfiguration configuration) :
            base(configuration)
        {
        }
        /// <inheritdoc/>
        public override void PostConfigure(string? name, IoTHubDeviceOptions options)
        {
        }
    }
}
