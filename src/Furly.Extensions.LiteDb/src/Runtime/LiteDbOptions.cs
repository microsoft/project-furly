// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb
{
    /// <summary>
    /// Configuration for Lite db
    /// </summary>
    public class LiteDbOptions
    {
        /// <summary>
        /// Connection string to use
        /// </summary>
        public string? DbConnectionString { get; set; }
    }
}
