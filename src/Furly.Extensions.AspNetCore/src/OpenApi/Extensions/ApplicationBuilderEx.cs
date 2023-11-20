// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.OpenApi.Models
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Furly.Extensions.AspNetCore.OpenApi;
    using System.Collections.Generic;

    /// <summary>
    /// Enable OpenApi
    /// </summary>
    public static class ApplicationBuilderEx
    {
        /// <summary>
        /// Use swagger in application
        /// </summary>
        /// <param name="app"></param>
        public static IApplicationBuilder UseSwagger(this IApplicationBuilder app)
        {
            var config = app.ApplicationServices.GetRequiredService<IOptions<OpenApiOptions>>();

            // Enable swagger and swagger ui
            app = app.UseSwagger(options =>
            {
                options.PreSerializeFilters.Add((doc, request) =>
                {
                    doc.Servers = new List<OpenApiServer>();
                    foreach (var scheme in new HashSet<string> { "https", request.Scheme })
                    {
                        var url = $"{scheme}://{request.Host.Value}";

                        // If config.OpenApiServerHost is set, we will use that instead of request.Host.Value
                        if (!string.IsNullOrEmpty(config.Value.OpenApiServerHost))
                        {
                            url = $"{scheme}://{config.Value.OpenApiServerHost}";
                        }

                        doc.Servers.Add(new OpenApiServer
                        {
                            Description = $"{scheme} endpoint.",
                            Url = url
                        });
                    }

                    // If request.PathBase exists, then we will prepend it to doc.Paths.
                    if (request.PathBase.HasValue)
                    {
                        var pathBase = request.PathBase.Value;
                        var prefixedPaths = new OpenApiPaths();
                        foreach (var path in doc.Paths)
                        {
                            prefixedPaths.Add(pathBase + path.Key, path.Value);
                        }
                        doc.Paths = prefixedPaths;
                    }
                });
                options.SerializeAsV2 = true;
                options.RouteTemplate = "swagger/{documentName}/openapi.json";
            });
            if (!config.Value.UIEnabled)
            {
                return app;
            }

            var api = app.ApplicationServices.GetRequiredService<IActionDescriptorCollectionProvider>();
            var infos = api.GetOpenApiInfos(null, null);

            // Where to host the ui
            return app.UseSwaggerUI(options =>
            {
                foreach (var info in infos)
                {
                    options.SwaggerEndpoint($"{info.Version}/openapi.json",
                        info.Version);
                }
            });
        }
    }
}
