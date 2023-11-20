// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Mqtt.Clients;
    using Furly.Extensions.Mqtt.Runtime;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddMqttClient(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IEventClient, MqttClient>()
                .AddScoped<IEventSubscriber, MqttClient>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<MqttOptions>, MqttConfig>()
                ;
        }

        /// <summary>
        /// Add server
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddMqttServer(this IServiceCollection services)
        {
            return services
                .AddLogging()
                .AddScoped<IEventClient, MqttServer>()
                .AddScoped<IEventSubscriber, MqttServer>()
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<MqttOptions>, MqttConfig>()
                ;
        }
    }
}
