// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.OpenApi
{
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System;

    /// <summary>
    /// Add extensions for autorest to schemas
    /// </summary>
    internal class ApiVersionExtensions : IDocumentFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Paths == null)
            {
                return;
            }
            var paths = new OpenApiPaths();
            foreach (var path in swaggerDoc.Paths)
            {
                paths.Add(path.Key.Replace("v{version}",
                    swaggerDoc.Info?.Version, StringComparison.Ordinal), path.Value);
            }
            swaggerDoc.Paths = paths;
        }
    }
}
