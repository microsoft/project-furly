// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// CouchDb configuration
    /// </summary>
    internal sealed class CouchDbConfig : PostConfigureOptionBase<CouchDbOptions>
    {
        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="configuration"></param>
        public CouchDbConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, CouchDbOptions options)
        {
            if (string.IsNullOrEmpty(options.HostName))
            {
                options.HostName =
                    GetStringOrDefault(EnvironmentVariable.COUCHDBHOSTNAME, "localhost");
            }
            if (string.IsNullOrEmpty(options.UserName))
            {
                options.UserName =
                    GetStringOrDefault(EnvironmentVariable.COUCHDBUSERNAME, "admin");
            }
            if (string.IsNullOrEmpty(options.Key))
            {
                options.Key =
                    GetStringOrDefault(EnvironmentVariable.COUCHDBKEY, "couchdb");
            }
        }
    }
}
