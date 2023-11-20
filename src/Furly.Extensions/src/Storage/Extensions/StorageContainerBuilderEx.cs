// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Storage.Runtime;
    using Furly.Extensions.Storage.Services;

    /// <summary>
    /// Database
    /// </summary>
    public static class StorageContainerBuilderEx
    {
        /// <summary>
        /// Add collection factory
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddCollectionFactory(this ContainerBuilder builder)
        {
            builder.RegisterType<CollectionFactory>()
                .AsImplementedInterfaces();
            builder.RegisterType<CollectionFactoryConfig>()
                .AsImplementedInterfaces();

            return builder;
        }

        /// <summary>
        /// Add memory key value store
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddMemoryKeyValueStore(this ContainerBuilder builder)
        {
            builder.RegisterType<MemoryKVStore>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }
    }
}
