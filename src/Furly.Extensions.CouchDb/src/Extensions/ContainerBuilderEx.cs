// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.CouchDb.Clients;
    using Furly.Extensions.CouchDb.Runtime;

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
        public static ContainerBuilder AddCouchDbClient(this ContainerBuilder builder)
        {
            builder.RegisterType<CouchDbClient>()
                .AsImplementedInterfaces();
            builder.AddOptions();
            builder.RegisterType<CouchDbConfig>()
                .AsImplementedInterfaces();

            return builder;
        }
    }
}
