// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly;
    using Furly.Extensions.Dapr.Clients;
    using Furly.Extensions.Dapr.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Storage;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add dapr pub sub client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddDaprPubSubClient(this ContainerBuilder builder)
        {
            builder.RegisterType<DaprPubSubClient>()
                .As<IEventClient>();

            builder.AddOptions();
            builder.RegisterType<DaprConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add dapr state store client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddDaprStateStoreClient(this ContainerBuilder builder)
        {
            builder.RegisterType<DaprStateStoreClient>()
                .As<IKeyValueStore>()
                .As<IAwaitable>()
                .As<IAwaitable<IKeyValueStore>>()
                .SingleInstance();
            builder.AddDefaultJsonSerializer();
            builder.AddOptions();
            builder.RegisterType<DaprConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
