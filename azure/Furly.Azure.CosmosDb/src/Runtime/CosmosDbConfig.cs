// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// CosmosDb configuration
    /// </summary>
    internal sealed class CosmosDbConfig : PostConfigureOptionBase<CosmosDbOptions>
    {
        /// <inheritdoc/>
        public CosmosDbConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, CosmosDbOptions options)
        {
            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                options.ConnectionString =
                    GetStringOrDefault(EnvironmentVariables.PCS_COSMOSDB_CONNSTRING,
                    GetStringOrDefault("PCS_STORAGEADAPTER_DOCUMENTDB_CONNSTRING",
                    GetStringOrDefault("PCS_TELEMETRY_DOCUMENTDB_CONNSTRING",
                    GetStringOrDefault("_DB_CS", string.Empty))));
            }
            options.ThroughputUnits ??=
                    GetIntOrDefault(EnvironmentVariables.PCS_COSMOSDB_THROUGHPUT, 400);
        }
    }
}
