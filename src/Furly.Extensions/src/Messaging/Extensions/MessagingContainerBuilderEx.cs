// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Messaging.Clients;

    /// <summary>
    /// Database
    /// </summary>
    public static class MessagingContainerBuilderEx
    {
        /// <summary>
        /// Add File system event client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddFileSystemEventClient(this ContainerBuilder builder)
        {
            builder.RegisterType<FileSystemEventClient>()
                .As<IEventClient>();
            return builder;
        }

        /// <summary>
        /// Add Http event client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddHttpEventClient(this ContainerBuilder builder)
        {
            builder.RegisterType<HttpEventClient>()
                .As<IEventClient>();
            return builder;
        }

        /// <summary>
        /// Add Null event client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddNullEventClient(this ContainerBuilder builder)
        {
            builder.RegisterType<NullEventClient>()
                .As<IEventClient>();
            return builder;
        }
    }
}
