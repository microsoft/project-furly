// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;
    using Furly.Extensions.CouchDb;
    using Furly.Extensions.CouchDb.Clients;
    using Furly.Extensions.CouchDb.Runtime;
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
        public static IServiceCollection AddCouchDbClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IDatabaseServer, CouchDbClient>()
                .AddSingleton<IHealthCheck, CouchDbClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<CouchDbOptions>, CouchDbConfig>()
                ;
        }
    }
}
