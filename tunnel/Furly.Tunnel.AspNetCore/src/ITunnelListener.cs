// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore
{
    using Microsoft.AspNetCore.Http;
    using System;

    /// <summary>
    /// Server
    /// </summary>
    internal interface ITunnelListener
    {
        /// <summary>
        /// Start server
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="request"></param>
        void Start(IServiceProvider provider, RequestDelegate request);
    }
}
