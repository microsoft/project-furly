// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Runtime
{
    using Furly.Extensions.Mqtt;
    using System;

    /// <summary>
    /// Aio options
    /// </summary>
    public sealed record class AioOptions
    {
        /// <summary>
        /// Connector id
        /// </summary>
        public string? ConnectorId { get; set; }

        /// <summary>
        /// Mqtt options
        /// </summary>
        public MqttOptions Mqtt { get; } = new MqttOptions
        {
            Protocol = MqttVersion.v5,
            QoS = Extensions.Messaging.QoS.AtLeastOnce
        };

        /// <summary>
        /// Identity used to compain over
        /// </summary>
        public string? Identity { get; set; }

        /// <summary>
        /// Name of the connector or workload
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Length to stay leader
        /// </summary>
        public TimeSpan LeadershipTermLength { get; set; }
            = TimeSpan.FromHours(24);

        /// <summary>
        /// How often to check for leadership
        /// </summary>
        public TimeSpan LeadershipRenewalPeriod { get; set; }
            = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Hook the trace listener to log to the logger
        /// </summary>
        public bool HookAioSdkTraceLogging { get; set; }
    }
}
