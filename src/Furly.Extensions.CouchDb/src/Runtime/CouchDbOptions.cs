// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb
{
    /// <summary>
    /// Configuration for Couchdb
    /// </summary>
    public class CouchDbOptions
    {
        /// <summary>
        /// Host to use
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// User name
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Key to use
        /// </summary>
        public string? Key { get; set; }
    }
}
