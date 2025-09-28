// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly;
    using Furly.Azure.IoT.Edge;
    using Furly.Azure.IoT.Edge.Runtime;
    using Furly.Azure.IoT.Edge.Services;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Storage;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add edge services
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddIoTEdgeServices(this IServiceCollection services)
        {
            return services
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<IoTEdgeClientOptions>, IoTEdgeClientConfig>()
                .AddScoped<IIoTEdgeDeviceIdentity, IoTEdgeIdentity>()
                .AddSingleton<IoTEdgeTwinClient>()
                .AddSingleton<IIoTEdgeTwinClient>(services => services.GetRequiredService<IoTEdgeTwinClient>())
                .AddSingleton<IKeyValueStore>(services => services.GetRequiredService<IoTEdgeTwinClient>())
                .AddSingleton<IAwaitable<IKeyValueStore>>(services => services.GetRequiredService<IoTEdgeTwinClient>())
                .AddSingleton<IIoTEdgeDeviceClient, IoTEdgeHubSdkClient>()
                .AddSingleton<IEventClient, IoTEdgeEventClient>()
                .AddSingleton<IEventClientFactory, IoTEdgeClientFactory>()
                .AddScoped<IEventSubscriber, IoTEdgeEventClient>()
                .AddScoped<IProcessIdentity, IoTEdgeEventClient>()
                .AddScoped<IIoTEdgeWorkloadApi, IoTEdgeWorkloadApi>()
                .AddScoped<IRpcClient, IoTEdgeRpcClient>()
                .AddScoped<IRpcServer, IoTEdgeRpcServer>()
                ;
        }
    }
}
