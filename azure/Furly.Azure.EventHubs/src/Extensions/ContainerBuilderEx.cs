// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Azure.EventHubs.Clients;
    using Furly.Azure.EventHubs.Runtime;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add event client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddHubEventClient(this ContainerBuilder builder)
        {
            // Client
            builder.AddOptions();
            builder.AddDefaultAzureCredentials();
            builder.RegisterType<EventHubsClient>()
                .AsImplementedInterfaces();
            builder.RegisterType<EventHubsClientFactory>()
                .AsImplementedInterfaces();

            // Configuration
            builder.RegisterType<EventHubsClientConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
