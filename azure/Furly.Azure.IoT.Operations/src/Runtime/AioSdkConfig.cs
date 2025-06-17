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
    internal sealed class AioSdkConfig : PostConfigureOptionBase<MqttOptions>
    {
        /// <inheritdoc/>
        public AioSdkConfig(IConfiguration configuration, ILogger<AioSdkConfig> logger)
            : base(configuration)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, MqttOptions options)
        {
            MqttConnectionSettings? settings = null;
            if (KubernetesClientConfiguration.IsInCluster())
            {
                if (Environment.GetEnvironmentVariable("CONNECTOR_ID") != null)
                {
                    _logger.LogInformation("Running as AIO connector.");
                    // Running as connector
                    for (var i = 0; ; i++)
                    {
                        try
                        {
                            settings = ConnectorFileMountSettings.FromFileMount();
                        }
                        catch (Exception ex) when (i < 2) // Retry once
                        {
                            // Try again after a second
                            _logger.LogError(ex, "Failed to read connector configuration from file");
                            Thread.Sleep(1000);
                        }
                    }
                }
                else if (Environment.GetEnvironmentVariable("AIO_BROKER_HOSTNAME") != null)
                {
                    _logger.LogInformation("Running as AIO workload.");
                    settings = MqttConnectionSettings.FromEnvVars();
                }
            }

            if (settings == null)
            {
                var cs = GetStringOrDefault("AIO_MQTT_CS");
                if (cs != null)
                {
                    _logger.LogInformation("Configure using AIO connection string.");
                    settings = MqttConnectionSettings.FromConnectionString(cs);
                }
            }

            if (settings != null)
            {
                options.ClientId = settings.ClientId;
                options.ClientCertificateFile = settings.CertFile;
                options.ClientPrivateKeyFile = settings.KeyFile;
                options.UserName = settings.Username;
                options.PasswordFile = settings.PasswordFile;
                options.KeepAlivePeriod = settings.KeepAlive;
                options.SessionExpiry = settings.SessionExpiry;
                options.CleanStart = settings.CleanStart;
                options.HostName = settings.HostName;
                options.Port = settings.TcpPort;
                options.UseTls = settings.UseTls;
                options.IssuerCertFile = settings.CaFile;
                options.PrivateKeyPasswordFile = settings.KeyPasswordFile;
                options.SatAuthFile = settings.SatAuthFile;
            }
        }
        private readonly ILogger _logger;
    }
}
