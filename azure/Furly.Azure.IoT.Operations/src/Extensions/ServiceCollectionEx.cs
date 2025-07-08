// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Azure.Iot.Operations.Protocol;
    using Furly;
    using Furly.Azure.IoT.Operations.Runtime;
    using Furly.Azure.IoT.Operations.Services;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Storage;
    using k8s;
    using System;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add Azure IoT Operations services
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddAzureIoTOperations(this IServiceCollection services)
        {
            return services
                .AddTelemetryPublisher()
                .AddAdrClient()
                .AddSchemaRegistry()
                .AddStateStore()
                ;
        }

        /// <summary>
        /// Add dss client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddStateStore(this IServiceCollection services)
        {
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioDssClient>()
                .AddSingleton<IKeyValueStore>(services => services.GetRequiredService<AioDssClient>())
                .AddSingleton<IAwaitable<IKeyValueStore>>(services => services.GetRequiredService<AioDssClient>())
                ;
        }

        /// <summary>
        /// Add sr client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddSchemaRegistry(this IServiceCollection services)
        {
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioSrClient>()
                .AddSingleton<IAioSrClient>(services => services.GetRequiredService<AioSrClient>())
                .AddSingleton<ISchemaRegistry>(services => services.GetRequiredService<AioSrClient>())
                ;
        }

        /// <summary>
        /// Add adr client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddAdrClient(this IServiceCollection services)
        {
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioAdrClient>()
                .AddSingleton<IAioAdrClient>(services => services.GetRequiredService<AioAdrClient>())
                ;
        }

        /// <summary>
        /// Add adr client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTelemetryPublisher(this IServiceCollection services)
        {
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioPublisher>()
                .AddSingleton<IEventClient>(services => services.GetRequiredService<AioPublisher>())
                ;
        }

        /// <summary>
        /// Add Azure IoT Operations core
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddAzureIoTOperationsCore(this IServiceCollection services)
        {
            return services
                .AddOptions()
                .AddSingleton<ApplicationContext>()
                .AddSingleton<AioSdkConfig>()
                .AddSingleton<AioSdk>()
                .AddSingleton<AioMqttClient>()
                .AddSingleton<IMqttPubSubClient>(services => services.GetRequiredService<AioMqttClient>())
                .AddSingleton<IAwaitable<IMqttPubSubClient>>(services => services.GetRequiredService<AioMqttClient>())
                ;
        }

        /// <summary>
        /// Add leader election services if running in connector mode
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddLeaderElection(this IServiceCollection services)
        {
            if (!KubernetesClientConfiguration.IsInCluster() ||
                Environment.GetEnvironmentVariable(AioSdkConfig.ConnectorId) == null)
            {
                // Not running in aio connector mode
                return services;
            }
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioLeClient>()
                .AddSingleton<ILeaderElection>(services => services.GetRequiredService<AioLeClient>())
                ;
        }
    }
}
