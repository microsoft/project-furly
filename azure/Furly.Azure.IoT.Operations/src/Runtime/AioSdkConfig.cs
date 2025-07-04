// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Runtime
{
    using Furly.Exceptions;
    using Furly.Extensions.Configuration;
    using Furly.Extensions.Mqtt;
    using global::Azure.Iot.Operations.Connector.ConnectorConfigurations;
    using global::Azure.Iot.Operations.Protocol.Connection;
    using k8s;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Azure IoT Operations configuration
    /// </summary>
    internal sealed class AioSdkConfig : PostConfigureOptionBase<AioOptions>
    {
        public const string Name = "AioName";
        public const string Identity = "AioIdentity";
        public const string ConnectorId = "CONNECTOR_ID";
        public const string BrokerHostName = "AIO_BROKER_HOSTNAME";
        public const string MqttConnectionString = "AIO_MQTT_CS";

        /// <inheritdoc/>
        public AioSdkConfig(IConfiguration configuration, ILogger<AioSdkConfig> logger)
            : base(configuration)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, AioOptions options)
        {
            MqttConnectionSettings? settings = null;
            if (KubernetesClientConfiguration.IsInCluster())
            {
                if (Environment.GetEnvironmentVariable(ConnectorId) != null)
                {
                    // Running as connector
                    for (var i = 0; ; i++)
                    {
                        try
                        {
                            settings = ConnectorFileMountSettings.FromFileMount();
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.FailedToReadConfig(ex);
                            if (i > 1)
                            {
                                throw;
                            }
                        }
                        // Try again after a second
                        Thread.Sleep(1000);
                    }
                }
                else if (Environment.GetEnvironmentVariable(BrokerHostName) != null)
                {
                    settings = MqttConnectionSettings.FromEnvVars();
                }
            }

            if (settings == null)
            {
                var cs = GetStringOrDefault(MqttConnectionString);
                if (cs != null)
                {
                    settings = MqttConnectionSettings.FromConnectionString(cs);
                }
            }

            if (settings != null)
            {
                options.Mqtt.ClientId = settings.ClientId;
                options.Mqtt.ClientCertificateFile = settings.CertFile;
                options.Mqtt.ClientCertificate = settings.ClientCertificate;
                options.Mqtt.ClientPrivateKeyFile = settings.KeyFile;
                options.Mqtt.PrivateKeyPasswordFile = settings.KeyPasswordFile;
                options.Mqtt.UserName = settings.Username;
                options.Mqtt.PasswordFile = settings.PasswordFile;
                options.Mqtt.KeepAlivePeriod = settings.KeepAlive;
                options.Mqtt.SessionExpiry = settings.SessionExpiry;
                options.Mqtt.CleanStart = settings.CleanStart;
                options.Mqtt.HostName = settings.HostName;
                options.Mqtt.Port = settings.TcpPort;
                options.Mqtt.UseTls = settings.UseTls;
                options.Mqtt.IssuerCertFile = settings.CaFile;
                options.Mqtt.TrustChain = settings.TrustChain;
                options.Mqtt.RequireRevocationCheck = false;
                options.Mqtt.ReceiveMaximum = settings.ReceiveMaximum;
                options.Mqtt.SatAuthFile = settings.SatAuthFile;
            }

            options.ConnectorId ??= GetStringOrDefault(ConnectorId) ??
                Environment.GetEnvironmentVariable(ConnectorId);
            options.Name ??= GetStringOrDefault(Name);
            options.Identity ??= GetStringOrDefault(Identity);
        }

        private readonly ILogger _logger;
    }

    /// <summary>
    /// Source-generated logging for AioSdkConfig
    /// </summary>
    internal static partial class AioSdkConfigLogging
    {
        private const int EventClass = 0;

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Error,
            Message = "Failed to read connector configuration from file")]
        public static partial void FailedToReadConfig(this ILogger logger, Exception ex);
    }
}
