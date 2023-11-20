// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Tunnel.AspNetCore;
    using Furly.Tunnel.AspNetCore.Services;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add http tunnel feature
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddHttpTunnel(
            this IServiceCollection services)
        {
            return services
                .AddDefaultJsonSerializer()
                .AddScoped<ITunnelListener, HttpTunnelMethodServerListener>()
                .AddScoped<ITunnelListener, HttpTunnelEventServerListener>();
        }
    }
}
