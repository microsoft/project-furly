// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.LiteDb.Clients;
    using Furly.Extensions.LiteDb.Runtime;

    /// <summary>
    /// Injected LiteDb client
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Load the module
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddLiteDb(this ContainerBuilder builder)
        {
            builder.RegisterType<LiteDbClient>().InstancePerLifetimeScope()
                .AsImplementedInterfaces();
            builder.AddOptions();
            builder.RegisterType<LiteDbConfig>()
                .AsImplementedInterfaces();

            return builder;
        }
    }
}
