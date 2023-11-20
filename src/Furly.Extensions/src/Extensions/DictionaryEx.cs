// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    using System.Linq;

    /// <summary>
    /// Dictionary extensions
    /// </summary>
    public static class DictionaryEx
    {
        /// <summary>
        /// Safe dictionary equals
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="that"></param>
        /// <param name="equality"></param>
        public static bool DictionaryEqualsSafe<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue>? dict, IReadOnlyDictionary<TKey, TValue>? that,
            Func<TValue, TValue, bool> equality)
        {
            if (dict == that)
            {
                return true;
            }
            if (dict == null || that == null)
            {
                return false;
            }
            if (dict.Count != that.Count)
            {
                return false;
            }
            return that.All(kv => dict.TryGetValue(kv.Key, out var v) &&
                equality(kv.Value, v));
        }

        /// <summary>
        /// Safe dictionary equals
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="that"></param>
        /// <param name="equality"></param>
        public static bool DictionaryEqualsSafe<TKey, TValue>(
            this IDictionary<TKey, TValue>? dict, IDictionary<TKey, TValue>? that,
            Func<TValue, TValue, bool> equality)
        {
            if (dict == that)
            {
                return true;
            }
            if (dict == null || that == null)
            {
                return false;
            }
            if (dict.Count != that.Count)
            {
                return false;
            }
            return that.All(kv => dict.TryGetValue(kv.Key, out var v) &&
                equality(kv.Value, v));
        }

        /// <summary>
        /// Safe dictionary equals
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="that"></param>
        public static bool DictionaryEqualsSafe<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue>? dict,
            IReadOnlyDictionary<TKey, TValue>? that)
        {
            return DictionaryEqualsSafe(dict, that, (x, y) => x.EqualsSafe(y));
        }

        /// <summary>
        /// Safe dictionary equals
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="that"></param>
        public static bool DictionaryEqualsSafe<TKey, TValue>(this IDictionary<TKey, TValue>? dict,
            IDictionary<TKey, TValue>? that)
        {
            return DictionaryEqualsSafe(dict, that, (x, y) => x.EqualsSafe(y));
        }

        /// <summary>
        /// Add or update item
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dict,
            TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }
    }
}
