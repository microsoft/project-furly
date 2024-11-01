// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Azure.IoT.Runtime;
    using Furly.Azure.IoT.Services;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add IoT Hub support
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubServices(this ContainerBuilder builder)
        {
            builder.AddIoTHubServiceClient();
            builder.AddIoTHubEventSubscriber();
            builder.AddIoTHubRpcClient();
            builder.AddIoTHubEventClient();
            builder.AddIoTHubModuleDeployer();
            return builder;
        }

        /// <summary>
        /// Add service client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubServiceClient(this ContainerBuilder builder)
        {
            // Clients
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();
            builder.RegisterType<IoTHubServiceClient>()
                .AsImplementedInterfaces();
            builder.RegisterType<IoTHubServiceConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// module deployer
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubModuleDeployer(this ContainerBuilder builder)
        {
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();
            builder.RegisterType<IoTHubModuleDeployer>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }

        /// <summary>
        /// Add rpc client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubRpcClient(this ContainerBuilder builder)
        {
            // Clients
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();
            builder.RegisterType<IoTHubRpcClient>()
                .AsImplementedInterfaces();
            builder.RegisterType<IoTHubServiceConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add subscriber
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubEventSubscriber(this ContainerBuilder builder)
        {
            // Clients
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();
            builder.RegisterType<IoTHubEventSubscriber>()
                .AsImplementedInterfaces();
            // Requires processor
            builder.RegisterType<IoTHubEventProcessor>()
                .AsImplementedInterfaces().SingleInstance();

            // Configuration
            builder.RegisterType<IoTHubServiceConfig>()
                .AsImplementedInterfaces();
            builder.RegisterType<IoTHubEventProcessorConfig>()
                .AsImplementedInterfaces();
            builder.RegisterType<StorageConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add event client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTHubEventClient(this ContainerBuilder builder)
        {
            // Client
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();

            builder.RegisterType<IoTHubEventClient>()
                .AsImplementedInterfaces();

            // Configuration
            builder.RegisterType<IoTHubServiceConfig>()
                .AsImplementedInterfaces();
            builder.RegisterType<IoTHubDeviceConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
