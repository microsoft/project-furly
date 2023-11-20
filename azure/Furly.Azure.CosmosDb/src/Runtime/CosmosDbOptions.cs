// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Configuration for cosmos db
    /// </summary>
    public class CosmosDbOptions
    {
        /// <summary>
        /// Connection string to use (mandatory)
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Throughput units (optional)
        /// </summary>
        public int? ThroughputUnits { get; set; }

        /// <summary>
        /// Consistency level (optional)
        /// </summary>
        public ConsistencyLevel? Consistency { get; internal set; }
    }
}
