// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Logging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Furly.Tunnel.Router;
    using Furly.Tunnel.Router.Services;
    using System;

    /// <summary>
    /// DI extension
    /// </summary>
    public static partial class ServiceCollectionEx
    {
        /// <summary>
        /// Add client handler
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection AddMethodRouter(
            this IServiceCollection services, Action<RouterOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }
            return services.AddScoped(
                s =>
                {
                    return new MethodRouter(s.GetServices<IRpcServer>(),
                        s.GetRequiredService<IJsonSerializer>(),
                        s.GetRequiredService<ILogger<MethodRouter>>())
                    {
                        Controllers = s.GetServices<IMethodController>()
                    };
                });
        }
    }
}
