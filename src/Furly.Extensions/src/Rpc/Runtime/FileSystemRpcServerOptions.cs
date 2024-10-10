// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc.Runtime
{
    /// <summary>
    /// File system options
    /// </summary>
    public class FileSystemRpcServerOptions
    {
        /// <summary>
        /// Request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// Request file extensions
        /// </summary>
        public string? RequestExtension { get; set; }

        /// <summary>
        /// Response path
        /// </summary>
        public string? ResponsePath { get; set; }

        /// <summary>
        /// Response file extension
        /// </summary>
        public string? ResponseExtension { get; set; }
    }
}
