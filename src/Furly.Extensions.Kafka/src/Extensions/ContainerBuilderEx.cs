// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Kafka.Clients;
    using Furly.Extensions.Kafka.Runtime;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add producer
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddKafkaProducerClient(this ContainerBuilder builder)
        {
            builder.AddKafkaAdminClient();
            builder.RegisterType<KafkaProducerClient>()
                .AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<KafkaProducerConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add consumer client
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddKafkaConsumerClient(this ContainerBuilder builder)
        {
            builder.AddKafkaAdminClient();
            builder.RegisterType<KafkaConsumerClient>()
                .AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<KafkaConsumerConfig>()
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Add admin
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddKafkaAdminClient(this ContainerBuilder builder)
        {
            builder.RegisterType<KafkaAdminClient>()
                .AsImplementedInterfaces().InstancePerLifetimeScope();

            builder.AddOptions();
            builder.RegisterType<KafkaServerConfig>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
