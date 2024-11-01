// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs
{
    /// <summary>
    /// Configuration for service
    /// </summary>
    public class SchemaRegistryOptions
    {
        /// <summary>
        /// Connection string
        /// </summary>
        public required string FullyQualifiedNamespace { get; set; }

        /// <summary>
        /// Schema group name in the schema registry
        /// Set to null to disable publishing schemas
        /// </summary>
        public required string SchemaGroupName { get; set; }
    }
}
