﻿// ------------------------------------------------------------
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
                .AddAdrClient()
                .AddSchemaRegistry()
                .AddLeaderElection()
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
                .AddSingleton<ISchemaRegistry>(services => services.GetRequiredService<AioSrClient>())
                ;
        }

        /// <summary>
        /// Add sr client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddLeaderElection(this IServiceCollection services)
        {
            return services
                .AddAzureIoTOperationsCore()
                .AddSingleton<AioLeClient>()
                .AddSingleton<ILeaderElection>(services => services.GetRequiredService<AioLeClient>())
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
                .AddSingleton<IAwaitable<IMqttPubSubClient>>(services => services.GetRequiredService<AioMqttClient>())
                ;
        }
    }
}
