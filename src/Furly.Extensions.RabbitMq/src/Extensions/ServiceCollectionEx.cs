// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.RabbitMq;
    using Furly.Extensions.RabbitMq.Clients;
    using Furly.Extensions.RabbitMq.Runtime;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add topic client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddRabbitMqBrokerClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IEventClient, RabbitMqBrokerClient>()
                .AddScoped<IEventSubscriber, RabbitMqBrokerClient>()
                .AddScoped<IRabbitMqConnection, RabbitMqConnection>()
                .AddSingleton<IHealthCheck, RabbitMqHealthCheck>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<RabbitMqOptions>, RabbitMqConfig>()
                ;
        }

        /// <summary>
        /// Add queue client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddRabbitMqQueueClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IEventClient, RabbitMqQueueClient>()
                .AddScoped<IEventSubscriber, RabbitMqQueueConsumer>()
                .AddScoped<IRabbitMqConnection, RabbitMqConnection>()
                .AddSingleton<IHealthCheck, RabbitMqHealthCheck>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<RabbitMqOptions>, RabbitMqConfig>()
                ;
        }
    }
}
