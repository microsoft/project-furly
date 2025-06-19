// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt
{
    using Furly.Extensions.Mqtt.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using MQTTnet;
    using System;

    /// <summary>
    /// Mqtt configuration
    /// </summary>
    public record class MqttOptions
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
        /// Password file
        /// </summary>
        public string? PasswordFile { get; set; }

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

        /// <summary>
        /// Allow setting client options
        /// </summary>
        public Action<MqttApplicationMessage>? ConfigureSchemaMessage { get; set; }

        /// <summary>
        /// Allow setting client options
        /// </summary>
        public Action<MqttClientOptions>? ConfigureMqttClient { get; set; }

        /// <summary>
        /// The maximum number of publishes, subscribes, or
        /// unsubscribes that will be allowed to be enqueued
        /// locally at a time.
        /// </summary>
        /// <remarks>
        /// Publishes, subscribes and unsubscribes all occupy
        /// separate queues, so this max value is for each of
        /// those queues..
        /// </remarks>
        public uint? MaxPendingMessages { get; set; }

        /// <summary>
        /// The strategy for the session client to use when
        /// deciding how to handle enqueueing a message when
        /// the queue is already full.
        /// </summary>
        public OverflowStrategy OverflowStrategy { get; set; }

        /// <summary>
        /// The retry policy that the session client will consult
        /// each time it attempts to reconnect and/or each time it
        /// attempts the initial connect.
        /// </summary>
        public IRetryPolicy? ConnectionRetryPolicy { get; set; }

        /// <summary>
        /// If true, this client will use the same retry policy when
        /// first connecting as it would during a reconnection.
        /// If false, this client will only make one attempt to
        /// connect when calling ConnectAsync.
        /// </summary>
        /// <remarks>
        /// Generally, this field should be set to true since you
        /// can expect mostly the same set of errors when initially
        /// connecting compared to when reconnecting. However, there
        /// are some exceptions that you are likely to see when
        /// initially connecting if you have a misconfiguration
        /// somewhere. This value is false by default so that these
        /// configuration errors are easier to catch.
        /// </remarks>
        public bool? RetryOnFirstConnect { get; set; }

        /// <summary>
        /// How long to wait for a single connection attempt to
        /// finish before abandoning it.
        /// </summary>
        /// <remarks>
        /// This value allows for you to configure the connection
        /// attempt timeout for both initial connection and reconnection
        /// scenarios. Note that this value is ignored for the initial
        /// connect attempt if <see cref="RetryOnFirstConnect"/> is false.
        /// </remarks>
        public TimeSpan? ConnectionAttemptTimeout { get; set; }

        /// <summary>
        /// Max request queue - default unbounded
        /// </summary>
        public int? MaxRequestQueue { get; set; }

        /// <summary>
        /// Clean start
        /// </summary>
        public bool? CleanStart { get; set; }

        /// <summary>
        /// Session expiry
        /// </summary>
        public TimeSpan? SessionExpiry { get; set; }

        /// <summary>
        /// Sat token file path
        /// </summary>
        public string? SatAuthFile { get; set; }

        /// <summary>
        /// Issuer certificate file
        /// </summary>
        public string? IssuerCertFile { get; set; }

        /// <summary>
        /// Require revocation check for issuer issued
        /// certificates.
        /// </summary>
        public bool? RequireRevocationCheck { get; set; }

        /// <summary>
        /// Client certificate file
        /// </summary>
        public string? ClientCertificateFile { get; set; }

        /// <summary>
        /// Certificate Key file
        /// </summary>
        public string? ClientPrivateKeyFile { get; set; }

        /// <summary>
        /// Password for the key file
        /// </summary>
        public string? PrivateKeyPasswordFile { get; set; }
    }
}
