// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel
{
    using Furly.Tunnel.Models;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Process tunnel requests and returns responses
    /// </summary>
    public interface ITunnelServer
    {
        /// <summary>
        /// Process request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<HttpTunnelResponseModel> ProcessAsync(HttpTunnelRequestModel request,
            CancellationToken ct = default);
    }
}
