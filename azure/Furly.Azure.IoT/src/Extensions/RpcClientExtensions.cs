// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc
{
    using Furly.Azure;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extend the rpc client apis for iot hub call.
    /// </summary>
    public static class RpcClientExtensions
    {
        /// <summary>
        /// Call method on device or module
        /// </summary>
        /// <param name="client"></param>
        /// <param name="deviceId"></param>
        /// <param name="method"></param>
        /// <param name="payload"></param>
        /// <param name="timeout"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static ValueTask<string> CallDeviceMethodAsync(this IRpcClient client,
            string deviceId, string method, string payload,
            TimeSpan? timeout = null, CancellationToken ct = default)
        {
            return client.CallMethodAsync(HubResource.Format(null,
                deviceId, null), method, payload, timeout, ct);
        }

        /// <summary>
        /// Call method on device or module
        /// </summary>
        /// <param name="client"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="method"></param>
        /// <param name="payload"></param>
        /// <param name="timeout"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static ValueTask<string> CallModuleMethodAsync(this IRpcClient client,
            string deviceId, string moduleId, string method, string payload,
            TimeSpan? timeout = null, CancellationToken ct = default)
        {
            return client.CallMethodAsync(HubResource.Format(null,
                deviceId, moduleId), method, payload, timeout, ct);
        }
    }
}
