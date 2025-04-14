// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Runtime
{
    using Furly.Extensions.Configuration;
    using Furly.Extensions.Mqtt;
    using global::Azure.Iot.Operations.Protocol.Connection;
    using k8s;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// Azure IoT Operations  configuration
    /// </summary>
    internal sealed class AioSdkConfig : PostConfigureOptionBase<MqttOptions>
    {
        /// <inheritdoc/>
        public AioSdkConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, MqttOptions options)
        {
            MqttConnectionSettings? settings = null;
            if (KubernetesClientConfiguration.IsInCluster() &&
                Environment.GetEnvironmentVariable("MQTT_HOST_NAME") != null)
            {
                settings = MqttConnectionSettings.FromEnvVars();
            }
            else
            {
                var cs = GetStringOrDefault("AIO_MQTT_CS");
                if (cs != null)
                {
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
    }
}
