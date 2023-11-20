// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Furly.Exceptions;
    using Furly.Extensions.Serializers;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Storage extensions
    /// </summary>
    public static class StorageExtensions
    {
        /// <summary>
        /// Invoke callback for each element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="feed"></param>
        /// <param name="callback"></param>
        /// <param name="ct"></param>
        public static async Task ForEachAsync<T>(this IResultFeed<T> feed,
            Func<T, Task> callback,
            CancellationToken ct = default)
        {
            while (feed.HasMore())
            {
                var results = await feed.ReadAsync(ct).ConfigureAwait(false);
                foreach (var item in results.ToList())
                {
                    await callback(item).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Count results in feed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="feed"></param>
        /// <param name="ct"></param>
        public static async Task<int> CountAsync<T>(this IResultFeed<T> feed,
            CancellationToken ct = default)
        {
            var count = 0;
            while (feed.HasMore())
            {
                var results = await feed.ReadAsync(ct).ConfigureAwait(false);
                count += results.Count();
            }
            return count;
        }

        /// <summary>
        /// Get value of a key. Unlike accessing the state store directly,
        /// this call will try to page in the value from the underlying store
        /// if it is not found in the state dictionary and if not found there
        /// will throw.
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <exception cref="ResourceNotFoundException">If value was not found.</exception>
        /// <returns></returns>
        public static async ValueTask<VariantValue> GetAsync(this IKeyValueStore store,
            string key, CancellationToken ct = default)
        {
            if (store.State.TryGetValue(key, out var value))
            {
                return value;
            }
            value = await store.TryPageInAsync(key, ct).ConfigureAwait(false);
            return value ?? throw new ResourceNotFoundException(
                $"Could not find value for key {key} in key value store.");
        }

        /// <summary>
        /// Find value of a key or returns null. This call will try to page in
        /// the value from the underlying store if it is not found in the
        /// state dictionary.
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async ValueTask<VariantValue?> FindAsync(this IKeyValueStore store,
            string key, CancellationToken ct = default)
        {
            if (!store.State.TryGetValue(key, out var value))
            {
                value = await store.TryPageInAsync(key, ct).ConfigureAwait(false);
            }
            return value;
        }
    }
}
