// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    /// <summary>
    /// List extensions
    /// </summary>
    public static class ListEx
    {
        private static readonly Random kRng = new();

        /// <summary>
        /// Shuffle list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <exception cref="ArgumentNullException"><paramref name="list"/> is <c>null</c>.</exception>
        public static IList<T> Shuffle<T>(this IList<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);
            var n = list.Count;
            while (n > 1)
            {
                n--;
#pragma warning disable CA5394 // Do not use insecure randomness
                var k = kRng.Next(n + 1);
#pragma warning restore CA5394 // Do not use insecure randomness
                (list[n], list[k]) = (list[k], list[n]);
            }
            return list;
        }

        /// <summary>
        /// Add range
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="range"></param>
        /// <exception cref="ArgumentNullException"><paramref name="list"/> is <c>null</c>.</exception>
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> range)
        {
            ArgumentNullException.ThrowIfNull(list);
            if (range == null)
            {
                return;
            }
            foreach (var item in range)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Foreach for list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="predicate"></param>
        /// <exception cref="ArgumentNullException"><paramref name="list"/> is <c>null</c>.</exception>
        public static void ForEach<T>(this IReadOnlyList<T> list, Action<T> predicate)
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(predicate);
            foreach (var item in list)
            {
                predicate(item);
            }
        }
    }
}
