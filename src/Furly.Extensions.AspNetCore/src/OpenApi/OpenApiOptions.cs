// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.OpenApi
{
    /// <summary>
    /// OpenApi / Swagger configuration
    /// </summary>
    public class OpenApiOptions
    {
        /// <summary>
        /// Whether openapi should be enabled
        /// </summary>
        public bool UIEnabled { get; set; }

        /// <summary>
        /// Open api version (v2 = json, v3 = yaml)
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Server host for openapi (optional)
        /// </summary>
        public string? OpenApiServerHost { get; set; }
    }
}
