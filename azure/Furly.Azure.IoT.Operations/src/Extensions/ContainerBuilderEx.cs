// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Azure.Iot.Operations.Protocol;
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Azure.IoT.Operations.Services;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add Azure IoT Operations services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddAzureIoTOperations(this ContainerBuilder builder)
        {
            builder.AddMqttClient();

            builder.RegisterType<ApplicationContext>()
                .AsSelf().SingleInstance();
            builder.RegisterType<AioSdk>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioSdkConfig>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioMqttClient>()
                .AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<AioDssClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioSrClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioAdrClient>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }
    }
}
