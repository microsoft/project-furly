// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Dapr;
    using Furly.Extensions.Dapr.Clients;
    using Furly.Extensions.Dapr.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Storage;
    using Furly;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add dapr pub sub client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IEventClient, DaprPubSubClient>()
                //     .AddScoped<IEventSubscriber, DaprEventClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<DaprOptions>, DaprConfig>()
                ;
        }

        /// <summary>
        /// Add dapr state store client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddDaprKeyValueStoreClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddDefaultJsonSerializer()
                .AddSingleton<DaprStateStoreClient>()
                .AddSingleton<IKeyValueStore>(
                    services => services.GetRequiredService<DaprStateStoreClient>())
                .AddSingleton<IAwaitable<IKeyValueStore>>(
                    services => services.GetRequiredService<DaprStateStoreClient>())
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<DaprOptions>, DaprConfig>()
                ;
        }
    }
}
