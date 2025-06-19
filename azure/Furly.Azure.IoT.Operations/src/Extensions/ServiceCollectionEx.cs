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
        public static IServiceCollection AddIoTEdgeServices(this IServiceCollection services)
        {
            return services
                .AddOptions()
                .AddMqttClient()
                .AddSingleton<ApplicationContext>()
                .AddSingleton<AioSdkConfig>()
                .AddSingleton<AioSdk>()
                .AddSingleton<AioMqttClient>()
                .AddSingleton<IAwaitable<IMqttPubSubClient>>(services => services.GetRequiredService<AioMqttClient>())
                .AddSingleton<AioAdrClient>()
                .AddSingleton<IAioAdrClient>(services => services.GetRequiredService<AioAdrClient>())
                .AddSingleton<AioSrClient>()
                .AddSingleton<ISchemaRegistry>(services => services.GetRequiredService<AioSrClient>())
                .AddSingleton<AioDssClient>()
                .AddSingleton<IKeyValueStore>(services => services.GetRequiredService<AioDssClient>())
                .AddSingleton<IAwaitable<IKeyValueStore>>(services => services.GetRequiredService<AioDssClient>())
                ;
        }
    }
}
