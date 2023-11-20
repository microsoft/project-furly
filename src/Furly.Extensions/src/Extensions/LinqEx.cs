// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Linq
{
    using System.Collections.Generic;

    /// <summary>
    /// Enumerable extensions
    /// </summary>
    public static class LinqEx
    {
        /// <summary>
        /// Create batches of enumerables
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="count"></param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items,
            int count)
        {
            if (count <= 0)
            {
                throw new ArgumentException("Cannot create 0 or negative size batches");
            }
            return items
                .Select((x, i) => Tuple.Create(x, i))
                .GroupBy(x => x.Item2 / count)
                .Select(g => g.Select(x => x.Item1));
        }

        /// <summary>
        /// Convert one item into an enumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public static IEnumerable<T> YieldReturn<T>(this T obj)
        {
            yield return obj;
        }
    }
}
