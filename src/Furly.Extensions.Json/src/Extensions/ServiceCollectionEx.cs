// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add a tunnel client
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDefaultJsonSerializer(
            this IServiceCollection services)
        {
            return services
                .AddSingleton<DefaultJsonSerializer>()
                .AddSingleton<ISerializer>(
                    x => x.GetRequiredService<DefaultJsonSerializer>())
                .AddSingleton<IJsonSerializer>(
                    x => x.GetRequiredService<DefaultJsonSerializer>())
                .AddSingleton<IJsonSerializerSettingsProvider>(
                    x => x.GetRequiredService<DefaultJsonSerializer>());
        }
    }
}
