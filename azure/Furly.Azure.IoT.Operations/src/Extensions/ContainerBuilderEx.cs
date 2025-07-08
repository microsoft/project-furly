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
            return builder
                .AddAzureIoTOperationsCore()
                .AddSchemaRegistry()
                .AddAdrClient()
                .AddTelemtryPublisher()
                .AddStateStore()
                ;
        }

        /// <summary>
        /// Add Azure IoT Operations state store services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddStateStore(this ContainerBuilder builder)
        {
            builder.AddAzureIoTOperationsCore();
            builder.RegisterType<AioDssClient>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }

        /// <summary>
        /// Add Azure IoT Operations schema registry services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddSchemaRegistry(this ContainerBuilder builder)
        {
            builder.AddAzureIoTOperationsCore();
            builder.RegisterType<AioSrClient>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }

        /// <summary>
        /// Add Azure IoT Operations ADR services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddAdrClient(this ContainerBuilder builder)
        {
            builder.AddAzureIoTOperationsCore();
            builder.RegisterType<AioAdrClient>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }

        /// <summary>
        /// Add Azure IoT Operations telemetry publisher services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddTelemtryPublisher(this ContainerBuilder builder)
        {
            builder.AddAzureIoTOperationsCore();
            builder.RegisterType<AioPublisher>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }

        /// <summary>
        /// Add azure iot operations core services
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static ContainerBuilder AddAzureIoTOperationsCore(this ContainerBuilder builder)
        {
            builder.AddOptions();
            builder.RegisterType<ApplicationContext>()
                .AsSelf().SingleInstance();
            builder.RegisterType<AioSdk>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioSdkConfig>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AioMqttClient>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }
    }
}
