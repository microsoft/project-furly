// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Google.Protobuf;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    public interface IDaprSidecarStorage
    {
        /// <summary>
        /// Disable query
        /// </summary>
        bool HasNoQuerySupport { get; set; }

        /// <summary>
        /// Get state store
        /// </summary>
        ConcurrentDictionary<string, ByteString> Items { get; }

        /// <summary>
        /// Wait until key is available or removed
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task WaitUntil(string key, bool available = true);
    }
}
