// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpsPolicy;
    using Furly.Extensions.AspNetCore.Hosting;
    using Furly.Extensions.AspNetCore.Hosting.Runtime;
    using Furly.Extensions.Hosting;
    using System;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Configure processing of forwarded headers
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddHeaderForwarding(this IServiceCollection services)
        {
            // No services associated

            services.AddOptions();
            services.AddTransient<IPostConfigureOptions<HeadersOptions>, HeadersConfig>();
            services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, HeadersConfig>();
            services.AddTransient<IConfigureNamedOptions<ForwardedHeadersOptions>, HeadersConfig>();
            return services;
        }

        /// <summary>
        /// Add https redirection
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddHttpsRedirect(this IServiceCollection services)
        {
            services.AddHsts(options =>
              {
                  options.Preload = true;
                  options.IncludeSubDomains = true;
                  options.MaxAge = TimeSpan.FromDays(60);
              });
            services.AddHttpsRedirection(_ => { });

            services.AddOptions();
            services.AddTransient<IPostConfigureOptions<WebHostOptions>, WebHostConfig>();
            services.AddTransient<IConfigureOptions<HttpsRedirectionOptions>, WebHostConfig>();
            services.AddTransient<IConfigureNamedOptions<HttpsRedirectionOptions>, WebHostConfig>();
            return services;
        }
    }
}
