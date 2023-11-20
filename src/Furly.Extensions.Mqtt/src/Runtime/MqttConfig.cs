// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// Mqtt configuration
    /// </summary>
    internal sealed class MqttConfig : PostConfigureOptionBase<MqttOptions>
    {
        /// <inheritdoc/>
        public MqttConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, MqttOptions options)
        {
            if (string.IsNullOrEmpty(options.HostName))
            {
                options.HostName = "localhost";
            }

            options.Port ??= (options.UseTls == true ? 8883 : 1883);
            options.QoS ??= Messaging.QoS.AtMostOnce;
            options.UseTls ??= options.Port != 1883;
            options.ClientId ??= Guid.NewGuid().ToString();

            if (options.ReconnectDelay == TimeSpan.Zero)
            {
                options.ReconnectDelay = null;
            }
        }
    }
}
