// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    /// <summary>
    /// Blob storage configuration
    /// </summary>
    public class StorageOptions
    {
        /// <summary>
        /// Storage endpoint
        /// </summary>
        public string? EndpointSuffix { get; set; }

        /// <summary>
        /// Storage account
        /// </summary>
        public string? AccountName { get; set; }

        /// <summary>
        /// Storage account key
        /// </summary>
        public string? AccountKey { get; set; }
    }
}
