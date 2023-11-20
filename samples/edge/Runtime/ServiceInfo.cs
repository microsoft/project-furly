// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.IoT.Edge.Service.Runtime
{
    using Furly.Extensions.Hosting;

    /// <summary>
    /// Service information
    /// </summary>
    public class ServiceInfo : IProcessIdentity
    {
        /// <inheritdoc/>
        public string Id => System.Guid.NewGuid().ToString();

        /// <inheritdoc/>
        public string Name => "Edge-Tunnel-Host";

        /// <inheritdoc/>
        public string Description => "Edge-Tunnel-Host";
    }
}
