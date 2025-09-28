// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Azure.EventHubs;
    using Furly.Azure.EventHubs.Clients;
    using Furly.Azure.EventHubs.Runtime;
    using Furly.Extensions.Messaging;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add event client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddEventClient(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddScoped<IEventClient, EventHubsClient>()
                .AddScoped<IEventClientFactory, EventHubsClientFactory>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<EventHubsClientOptions>, EventHubsClientConfig>()
                ;
        }
    }
}
