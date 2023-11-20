// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Furly.Extensions.Serializers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store interface
    /// </summary>
    public interface IKeyValueStore
    {
        /// <summary>
        /// Name of the storage interface used in the
        /// state store.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get kv store state which can be manipulated
        /// and which is flushed periodically.
        /// </summary>
        IDictionary<string, VariantValue> State { get; }

        /// <summary>
        /// Try page in
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<VariantValue?> TryPageInAsync(string key,
            CancellationToken ct = default);
    }
}
