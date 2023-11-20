// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Kafka;
    using Furly.Extensions.Kafka.Clients;
    using Furly.Extensions.Kafka.Runtime;
    using Furly.Extensions.Messaging;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add admin
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddKafkaAdminClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IKafkaAdminClient, KafkaAdminClient>()
                .AddSingleton<IHealthCheck, KafkaAdminClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<KafkaServerOptions>, KafkaServerConfig>()
                ;
        }

        /// <summary>
        /// Add producer
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddKafkaProducerClient(this IServiceCollection services)
        {
            return services
                .AddKafkaAdminClient()
                .AddScoped<IEventClient, KafkaProducerClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<KafkaProducerOptions>, KafkaProducerConfig>()
                ;
        }

        /// <summary>
        /// Add consumer
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddKafkaConsumerClient(this IServiceCollection services)
        {
            return services
                .AddKafkaAdminClient()
                .AddScoped<IEventSubscriber, KafkaConsumerClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<KafkaConsumerOptions>, KafkaConsumerConfig>()
                ;
        }
    }
}
