// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Builder;
    using Furly.Extensions.AspNetCore.Hosting;
    using Furly.Extensions.Hosting;

    /// <summary>
    /// Configure application builder
    /// </summary>
    public static class ApplicationBuilderEx
    {
        /// <summary>
        /// Use header forwarding
        /// </summary>
        /// <param name="app"></param>
        public static IApplicationBuilder UseHeaderForwarding(this IApplicationBuilder app)
        {
            var headerOptions = app.ApplicationServices.GetService<IOptions<HeadersOptions>>();
            if (headerOptions?.Value.ForwardingEnabled ?? false)
            {
                app = app.UseForwardedHeaders();
            }
            return app;
        }

        /// <summary>
        /// Use https redirection
        /// </summary>
        /// <param name="app"></param>
        public static IApplicationBuilder UsePathBase(this IApplicationBuilder app)
        {
            var pathOptions = app.ApplicationServices.GetService<IOptions<WebHostOptions>>();
            if (!string.IsNullOrEmpty(pathOptions?.Value.ServicePathBase))
            {
                app = app.UsePathBase(pathOptions.Value.ServicePathBase);
            }
            return app;
        }

        /// <summary>
        /// Use https redirection
        /// </summary>
        /// <param name="app"></param>
        public static IApplicationBuilder UseHttpsRedirect(this IApplicationBuilder app)
        {
            var httpsOptions = app.ApplicationServices.GetService<IOptions<WebHostOptions>>();
            if (httpsOptions?.Value.UseHttpsRedirect ?? false)
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }
            return app;
        }
    }
}
