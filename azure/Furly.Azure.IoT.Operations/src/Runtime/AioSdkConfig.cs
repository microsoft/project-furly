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
                    _logger.RunningAsConnector();
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
                            _logger.FailedToReadConfig(ex);
                            Thread.Sleep(1000);
                        }
                    }
                }
                else if (Environment.GetEnvironmentVariable("AIO_BROKER_HOSTNAME") != null)
                {
                    _logger.RunningAsWorkload();
                    settings = MqttConnectionSettings.FromEnvVars();
                }
            }

            if (settings == null)
            {
                var cs = GetStringOrDefault("AIO_MQTT_CS");
                if (cs != null)
                {
                    _logger.ConfigureUsingConnectionString();
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

    /// <summary>
    /// Source-generated logging for AioSdkConfig
    /// </summary>
    internal static partial class AioSdkConfigLogging
    {
        private const int EventClass = 0;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Running as AIO connector.")]
        public static partial void RunningAsConnector(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Error,
            Message = "Failed to read connector configuration from file")]
        public static partial void FailedToReadConfig(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Running as AIO workload.")]
        public static partial void RunningAsWorkload(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Information,
            Message = "Configure using AIO connection string.")]
        public static partial void ConfigureUsingConnectionString(this ILogger logger);
    }
}
