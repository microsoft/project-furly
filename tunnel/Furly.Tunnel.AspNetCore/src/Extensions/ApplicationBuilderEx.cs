// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.AspNetCore.Builder;
    using global::Furly.Tunnel.AspNetCore;

    /// <summary>
    /// Configure application builder
    /// </summary>
    public static class ApplicationBuilderEx
    {
        /// <summary>
        /// Start tunnel listener
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseHttpTunnel(this IApplicationBuilder app)
        {
            foreach (var server in app.ApplicationServices.GetServices<ITunnelListener>())
            {
                var requestDelegate = app.Build();
                server.Start(app.ApplicationServices, requestDelegate);
            }
            return app;
        }
    }
}
