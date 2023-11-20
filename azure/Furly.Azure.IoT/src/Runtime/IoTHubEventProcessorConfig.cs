// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Runtime
{
    using Furly.Azure.IoT;
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// IoT Hub Event processor configuration - wraps a configuration root
    /// </summary>
    public class IoTHubEventProcessorConfig : PostConfigureOptionBase<IoTHubEventProcessorOptions>
    {
        /// <inheritdoc/>
        public IoTHubEventProcessorConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, IoTHubEventProcessorOptions options)
        {
            if (string.IsNullOrEmpty(options.ConsumerGroup))
            {
                options.ConsumerGroup = GetStringOrDefault(
                    EnvironmentVariables.PCS_IOTHUB_EVENTHUBCONSUMERGROUP,
                    GetStringOrDefault("PCS_IOTHUBREACT_HUB_CONSUMERGROUP", "$default"));
            }

            if (options.EventHubEndpoint == null)
            {
                var ep = GetStringOrDefault(EnvironmentVariables.PCS_IOTHUB_EVENTHUBENDPOINT,
                    GetStringOrDefault("PCS_IOTHUBREACT_HUB_ENDPOINT", string.Empty));
                if (ep.StartsWith("Endpoint=", StringComparison.InvariantCultureIgnoreCase))
                {
                    ep = ep.Remove(0, "Endpoint=".Length);
                }
                options.EventHubEndpoint = ep;
            }

            var websocket = GetBoolOrNull("_WS");
            if (websocket != null)
            {
                options.UseWebsockets = websocket.Value;
            }

            if (options.ReceiveTimeout == TimeSpan.Zero)
            {
                options.ReceiveTimeout = TimeSpan.FromSeconds(5);
            }
#if DEBUG
            if (options.SkipEventsOlderThan == null)
            {
                options.SkipEventsOlderThan = TimeSpan.FromMinutes(5);
            }
#endif
            if (options.CheckpointInterval == null)
            {
                options.CheckpointInterval = TimeSpan.FromMinutes(1);
            }
        }
    }
}
