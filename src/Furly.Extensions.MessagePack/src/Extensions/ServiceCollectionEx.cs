// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.MessagePack;

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
        public static IServiceCollection AddMessagePackSerializer(
            this IServiceCollection services)
        {
            return services
                .AddSingleton<MessagePackSerializer>()
                .AddSingleton<ISerializer>(
                    x => x.GetRequiredService<MessagePackSerializer>())
                .AddSingleton<IBinarySerializer>(
                    x => x.GetRequiredService<MessagePackSerializer>())
                .AddSingleton<IMessagePackSerializerOptionsProvider>(
                    x => x.GetRequiredService<MessagePackSerializer>());
        }
    }
}
