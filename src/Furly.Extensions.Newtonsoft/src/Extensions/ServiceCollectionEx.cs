// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Newtonsoft;

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
        public static IServiceCollection AddDNewtonsoftJsonSerializer(
            this IServiceCollection services)
        {
            return services
                .AddSingleton<NewtonsoftJsonSerializer>()
                .AddSingleton<ISerializer>(
                    x => x.GetRequiredService<NewtonsoftJsonSerializer>())
                .AddSingleton<IJsonSerializer>(
                    x => x.GetRequiredService<NewtonsoftJsonSerializer>())
                .AddSingleton<INewtonsoftSerializerSettingsProvider>(
                    x => x.GetRequiredService<NewtonsoftJsonSerializer>());
        }
    }
}
