// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Azure.CosmosDb.Clients;
    using Furly.Azure.CosmosDb.Runtime;

    /// <summary>
    /// Injected CouchDb client
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add couchdb client
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static ContainerBuilder AddCosmosDbClient(this ContainerBuilder builder)
        {
            builder.RegisterType<CosmosDbServiceClient>()
                .AsImplementedInterfaces();
            builder.AddOptions();
            builder.RegisterType<CosmosDbConfig>()
                .AsImplementedInterfaces();

            return builder;
        }
    }
}
