// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Extensions.LiteDb;
    using Furly.Extensions.LiteDb.Clients;
    using Furly.Extensions.LiteDb.Runtime;
    using Furly.Extensions.Storage;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add litedb client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddLiteDb(this IServiceCollection services)
        {
            return services
                .AddScoped<IDatabaseServer, LiteDbClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<LiteDbOptions>, LiteDbConfig>()
                ;
        }
    }
}
