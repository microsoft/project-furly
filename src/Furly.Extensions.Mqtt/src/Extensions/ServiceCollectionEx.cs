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
                .AddScoped<MqttClient>()
                .AddScoped<IEventClient>(services => services.GetRequiredService<MqttClient>())
                .AddScoped<IEventSubscriber>(services => services.GetRequiredService<MqttClient>())
                .AddScoped<IManagedClient>(services => services.GetRequiredService<MqttClient>())
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
                .AddScoped<MqttServer>()
                .AddScoped<IEventClient>(services => services.GetRequiredService<MqttServer>())
                .AddScoped<IEventSubscriber>(services => services.GetRequiredService<MqttServer>())
                .AddOptions()
                .AddSingleton<IPostConfigureOptions<MqttOptions>, MqttConfig>()
                ;
        }
    }
}
