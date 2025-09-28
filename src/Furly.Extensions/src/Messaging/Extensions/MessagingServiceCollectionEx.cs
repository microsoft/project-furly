// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Messaging.Clients;
    using Furly.Extensions.Messaging.Runtime;
    using System;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class MessagingServiceCollectionEx
    {
        /// <summary>
        /// Add file system event client
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureOptions"></param>
        public static IServiceCollection AddFileSystemEventClient(this IServiceCollection services,
            Action<FileSystemEventClientOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            return services
                .AddScoped<IEventClient, FileSystemEventClient>()
                .AddScoped<IEventClientFactory, FileSystemClientFactory>()
                ;
        }

        /// <summary>
        /// Add http event client
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureOptions"></param>
        public static IServiceCollection AddHttpEventClient(this IServiceCollection services,
            Action<HttpEventClientOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            return services
                .AddScoped<IEventClient, HttpEventClient>()
                ;
        }

        /// <summary>
        /// Add null event client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddNullEventClient(this IServiceCollection services)
        {
            return services
                .AddScoped<IEventClient, NullEventClient>()
                ;
        }
    }
}
