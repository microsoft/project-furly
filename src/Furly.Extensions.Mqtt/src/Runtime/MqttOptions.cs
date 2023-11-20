// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt
{
    using Furly.Extensions.Messaging;
    using System;

    /// <summary>
    /// Mqtt configuration
    /// </summary>
    public class MqttOptions
    {
        /// <summary>
        /// Client identity
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Protocol to use (default is v5)
        /// </summary>
        public MqttVersion Protocol { get; set; }

        /// <summary>
        /// Host name of broker
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// Broker port
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Credential
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Quality of service
        /// </summary>
        public QoS? QoS { get; set; }

        /// <summary>
        /// Whether to use tls
        /// </summary>
        public bool? UseTls { get; set; }

        /// <summary>
        /// Whether to accept any certificate
        /// </summary>
        public bool? AllowUntrustedCertificates { get; set; }

        /// <summary>
        /// Path to use if web socket should be used
        /// (and is supported)
        /// </summary>
        public string? WebSocketPath { get; set; }

        /// <summary>
        /// Max payload sizes
        /// </summary>
        public int? MaxPayloadSize { get; set; }

        /// <summary>
        /// Reconnection delay
        /// </summary>
        public TimeSpan? ReconnectDelay { get; set; }

        /// <summary>
        /// Default method call timeout.
        /// </summary>
        public TimeSpan? DefaultMethodCallTimeout { get; set; }

        /// <summary>
        /// How many times to retry on method call timeouts
        /// before throwing exception.
        /// </summary>
        public int? MethodCallTimeoutRetries { get; set; }

        /// <summary>
        /// Keep alive timer duration. Set to Zero to
        /// turn off keep alives.
        /// </summary>
        public TimeSpan? KeepAlivePeriod { get; set; }

        /// <summary>
        /// Number of clients to create to partition
        /// topics across a broker's load balanced nodes.
        /// </summary>
        public int? NumberOfClientPartitions { get; set; }
    }
}
