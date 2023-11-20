// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Tunnel.Services;

    /// <summary>
    /// DI extension
    /// </summary>
    public static partial class ServiceCollectionEx
    {
        /// <summary>
        /// Add a tunnel client
        /// </summary>
        /// <param name="services"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IServiceCollection AddHttpClientWithTunnelOverMethodClient(
            this IServiceCollection services, string name)
        {
            return services.AddHttpClient(name)
                .WithHttpTunnelMethodClientHandler();
        }

        /// <summary>
        /// Add a tunnel client with event client handler
        /// </summary>
        /// <param name="services"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IServiceCollection AddHttpClientWithTunnelOverEventClient(
            this IServiceCollection services, string name)
        {
            return services.AddHttpClient(name)
                .WithHttpTunnelEventClientHandler();
        }

        /// <summary>
        /// Add a tunnel client with hybrid handler
        /// </summary>
        /// <param name="services"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IServiceCollection AddHttpClientWithTunnelOverHybridClient(
            this IServiceCollection services, string name)
        {
            return services.AddHttpClient(name)
                .WithHttpTunnelHybridHandler();
        }

        /// <summary>
        /// Add client handler
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection WithHttpTunnelMethodClientHandler(
            this IHttpClientBuilder services)
        {
            services.ConfigurePrimaryHttpMessageHandler(
                s => s.GetRequiredService<HttpTunnelMethodClientHandler>());
            return services.Services
                .AddDefaultJsonSerializer()
                .AddScoped<HttpTunnelMethodClientHandler>();
        }

        /// <summary>
        /// Add client handler
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection WithHttpTunnelHybridHandler(
            this IHttpClientBuilder services)
        {
            services.ConfigurePrimaryHttpMessageHandler(
                s => s.GetRequiredService<HttpTunnelHybridClientHandler>());
            return services.Services
                .AddDefaultJsonSerializer()
                .AddScoped<HttpTunnelHybridClientHandler>();
        }

        /// <summary>
        /// Add client handler
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection WithHttpTunnelEventClientHandler(
            this IHttpClientBuilder services)
        {
            services.ConfigurePrimaryHttpMessageHandler(
                s => s.GetRequiredService<HttpTunnelEventClientHandler>());
            return services.Services
                .AddDefaultJsonSerializer()
                .AddScoped<HttpTunnelEventClientHandler>();
        }
    }
}
