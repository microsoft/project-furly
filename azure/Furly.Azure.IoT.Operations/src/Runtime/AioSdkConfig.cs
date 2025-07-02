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
    internal sealed class AioSdkConfig : PostConfigureOptionBase<MqttOptions>,
        IPostConfigureOptions<AioOptions>
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
        public override void PostConfigure(string? name, MqttOptions options)
        {
            MqttConnectionSettings? settings = null;
            if (KubernetesClientConfiguration.IsInCluster())
            {
                if (Environment.GetEnvironmentVariable(ConnectorId) != null)
                {
                    _logger.RunningAsConnector();
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
                    _logger.RunningAsWorkload();
                    settings = MqttConnectionSettings.FromEnvVars();
                }
            }

            if (settings == null)
            {
                var cs = GetStringOrDefault(MqttConnectionString);
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

                _logger.ConfigurationLoaded();
            }
        }

        /// <inheritdoc/>
        public void PostConfigure(string? name, AioOptions options)
        {
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

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Running as Azure IoT Operations connector.")]
        public static partial void RunningAsConnector(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Error,
            Message = "Failed to read connector configuration from file")]
        public static partial void FailedToReadConfig(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Running as Azure IoT Operations workload.")]
        public static partial void RunningAsWorkload(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Information,
            Message = "Configure using Azure IoT Operations connection string.")]
        public static partial void ConfigureUsingConnectionString(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 4, Level = LogLevel.Information,
            Message = "Azure IoT Operations configuration loaded.")]
        public static partial void ConfigurationLoaded(this ILogger logger);
    }
}
