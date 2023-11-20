// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    /// <summary>
    /// Configure a specific container to open
    /// </summary>
    public class CollectionFactoryOptions
    {
        /// <summary>
        /// Name of database
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Name of container
        /// </summary>
        public string? ContainerName { get; set; }
    }
}
