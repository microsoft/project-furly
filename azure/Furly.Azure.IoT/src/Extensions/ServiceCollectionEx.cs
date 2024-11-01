// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Azure.IoT;
    using Furly.Azure.IoT.Runtime;
    using Furly.Azure.IoT.Services;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add IoT Hub support
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddIoTHubServices(this IServiceCollection services)
        {
            return services
                .AddIoTHubServiceClient()
                .AddIoTHubEventSubscriber()
                .AddIoTHubRpcClient()
                .AddIoTHubEventClient()
                ;
        }

        /// <summary>
        /// Add service client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddIoTHubServiceClient(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddScoped<IIoTHubTwinServices, IoTHubServiceClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<IoTHubServiceOptions>, IoTHubServiceConfig>()
                ;
        }

        /// <summary>
        /// Add rpc client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddIoTHubRpcClient(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddScoped<IRpcClient, IoTHubRpcClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<IoTHubServiceOptions>, IoTHubServiceConfig>()
                ;
        }

        /// <summary>
        /// Add event client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddIoTHubEventClient(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddScoped<IEventClient, IoTHubEventClient>()
                .AddScoped<IProcessIdentity, IoTHubEventClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<IoTHubDeviceOptions>, IoTHubDeviceConfig>()
                .AddSingleton<IPostConfigureOptions<IoTHubServiceOptions>, IoTHubServiceConfig>()
                ;
        }

        /// <summary>
        /// Add event subscriber
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddIoTHubEventSubscriber(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddScoped<IEventSubscriber, IoTHubEventSubscriber>()
                .AddSingleton<IIoTHubEventProcessor, IoTHubEventProcessor>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<IoTHubServiceOptions>, IoTHubServiceConfig>()
                .AddSingleton<IPostConfigureOptions<IoTHubEventProcessorOptions>, IoTHubEventProcessorConfig>()
                .AddSingleton<IPostConfigureOptions<StorageOptions>, StorageConfig>()
                ;
        }
    }
}
