// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Mqtt.Clients;
    using Furly.Extensions.Mqtt.Runtime;
    using System;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add client
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        public static ContainerBuilder AddMqttClient(this ContainerBuilder builder,
            Action<MqttOptions>? configure = null)
        {
            builder.RegisterType<MqttClient>()
                .AsImplementedInterfaces().SingleInstance();
            return ConfigureMqtt(builder, configure);
        }

        /// <summary>
        /// Add server
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        public static ContainerBuilder AddMqttServer(this ContainerBuilder builder,
            Action<MqttOptions>? configure = null)
        {
            builder.RegisterType<MqttServer>()
                .AsSelf()
                .AsImplementedInterfaces().SingleInstance();
            return ConfigureMqtt(builder, configure);
        }

        /// <summary>
        /// Configure options
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        public static ContainerBuilder ConfigureMqtt(this ContainerBuilder builder,
            Action<MqttOptions>? configure = null)
        {
            builder.AddOptions();
            if (configure != null)
            {
                builder.Configure(configure);
            }
            builder.RegisterType<MqttConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
