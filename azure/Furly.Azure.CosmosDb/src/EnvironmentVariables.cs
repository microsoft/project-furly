// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb
{
    /// <summary>
    /// Common runtime environment variables
    /// </summary>
    internal static class EnvironmentVariables
    {
        /// <summary> Connection string </summary>
        public const string PCS_COSMOSDB_CONNSTRING =
            "PCS_COSMOSDB_CONNSTRING";
        /// <summary> Configured throughput </summary>
        public const string PCS_COSMOSDB_THROUGHPUT =
            "PCS_COSMOSDB_THROUGHPUT";
    }
}
