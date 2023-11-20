// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.RabbitMq.Clients;
    using Furly.Extensions.RabbitMq.Runtime;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add topic client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddRabbitMqBrokerClient(this ContainerBuilder builder)
        {
            builder.RegisterType<RabbitMqBrokerClient>()
                .AsImplementedInterfaces();
            builder.RegisterType<RabbitMqConnection>()
                .AsImplementedInterfaces();
            builder.RegisterType<RabbitMqHealthCheck>()
                .AsImplementedInterfaces().SingleInstance();

            builder.AddOptions();
            builder.RegisterType<RabbitMqConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add queue client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddRabbitMqQueueClient(this ContainerBuilder builder)
        {
            builder.RegisterType<RabbitMqQueueClient>()
                .AsImplementedInterfaces();
            builder.RegisterType<RabbitMqQueueConsumer>()
                .AsImplementedInterfaces();
            builder.RegisterType<RabbitMqConnection>()
                .AsImplementedInterfaces();
            builder.RegisterType<RabbitMqHealthCheck>()
                .AsImplementedInterfaces().SingleInstance();

            builder.AddOptions();
            builder.RegisterType<RabbitMqConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
