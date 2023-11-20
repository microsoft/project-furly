// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Runtime
{
    using Furly.Azure.IoT.Edge;
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// IoT Edge device or module configuration
    /// </summary>
    internal sealed class IoTEdgeClientConfig : PostConfigureOptionBase<IoTEdgeClientOptions>
    {
        /// <inheritdoc/>
        public IoTEdgeClientConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, IoTEdgeClientOptions options)
        {
            if (string.IsNullOrEmpty(options.EdgeHubConnectionString))
            {
                options.EdgeHubConnectionString =
                    GetStringOrDefault(nameof(options.EdgeHubConnectionString));
            }
            if (options.Transport == 0)
            {
                options.Transport = (TransportOption)Enum.Parse(typeof(TransportOption),
                    GetStringOrDefault(nameof(options.Transport),
                        nameof(TransportOption.MqttOverTcp)), true);
            }
        }
    }
}
