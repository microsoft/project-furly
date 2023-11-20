// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.OpenApi.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// OpenApi configuration
    /// </summary>
    internal class OpenApiConfig : PostConfigureOptionBase<OpenApiOptions>
    {
        /// <inheritdoc/>
        public OpenApiConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, OpenApiOptions options)
        {
            options.UIEnabled = GetBoolOrDefault(EnvironmentVariable.OPENAPIENABLED, true);

            if (string.IsNullOrEmpty(options.OpenApiServerHost))
            {
                options.OpenApiServerHost =
                    GetStringOrDefault(EnvironmentVariable.OPENAPISERVERHOST)?.Trim();
            }

            if (options.SchemaVersion is not 2 and not 3)
            {
                var useV2 = GetBoolOrDefault(EnvironmentVariable.OPENAPIUSEV2, true);
                options.SchemaVersion = useV2 ? 2 : 3;
            }
        }
    }
}
