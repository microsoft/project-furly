// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Storage;
    using Furly.Extensions.Storage.Runtime;
    using Furly.Extensions.Storage.Services;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class StorageServiceCollectionEx
    {
        /// <summary>
        /// Add collection factory
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddCollectionFactory(this IServiceCollection services)
        {
            return services
                .AddScoped<ICollectionFactory, CollectionFactory>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<CollectionFactoryOptions>, CollectionFactoryConfig>()
                ;
        }

        /// <summary>
        /// Add memory key value store
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddMemoryKeyValueStore(this IServiceCollection services)
        {
            return services
                .AddSingleton<IKeyValueStore, MemoryKVStore>()
                ;
        }
    }
}
