// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Services
{
    using System;

    /// <summary>
    /// Options for method router instance
    /// </summary>
    public sealed class RouterOptions
    {
        /// <summary>
        /// Root topic to mount the method router to.
        /// </summary>
        public string? MountPoint { get; set; }

        /// <summary>
        /// Timeout of chunks not received by the
        /// chunk server.
        /// </summary>
        public TimeSpan? ChunkTimeout { get; set; }
    }
}
