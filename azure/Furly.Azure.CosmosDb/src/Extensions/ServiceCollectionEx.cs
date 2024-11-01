// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Azure.CosmosDb;
    using Furly.Azure.CosmosDb.Clients;
    using Furly.Azure.CosmosDb.Runtime;
    using Furly.Extensions.Storage;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add couchdb client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddCosmosDbClient(this IServiceCollection services)
        {
            return services
                .AddDefaultAzureCredentials()
                .AddLogging()
                .AddScoped<IDatabaseServer, CosmosDbServiceClient>()
                //     .AddSingleton<IHealthCheck, CosmosDbServiceClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<CosmosDbOptions>, CosmosDbConfig>()
                ;
        }
    }
}
