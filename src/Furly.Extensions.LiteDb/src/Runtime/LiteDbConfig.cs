// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// LiteDb configuration
    /// </summary>
    internal sealed class LiteDbConfig : PostConfigureOptionBase<LiteDbOptions>
    {
        /// <inheritdoc/>
        public LiteDbConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, LiteDbOptions options)
        {
            if (string.IsNullOrEmpty(options.DbConnectionString))
            {
                options.DbConnectionString = GetStringOrDefault(EnvironmentVariable.LITEDBCONNSTRING)
                    ?? GetStringOrDefault("_DB_CS");
            }
        }
    }
}
