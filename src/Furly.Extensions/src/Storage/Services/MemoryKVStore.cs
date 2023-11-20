// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage.Services
{
    using Furly.Extensions.Serializers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store in memory
    /// </summary>
    public sealed class MemoryKVStore : IKeyValueStore
    {
        /// <inheritdoc/>
        public string Name => "Memory";

        /// <inheritdoc/>
        public ValueTask<VariantValue?> TryPageInAsync(string key,
            CancellationToken ct)
        {
            State.TryGetValue(key, out var value);
            return ValueTask.FromResult(value);
        }

        /// <inheritdoc/>
        public IDictionary<string, VariantValue> State { get; }
            = new ConcurrentDictionary<string, VariantValue>();
    }
}
