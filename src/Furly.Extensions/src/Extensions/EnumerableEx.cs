// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    using System.Linq;

    /// <summary>
    /// Collection extensions
    /// </summary>
    public static class EnumerableEx
    {
        /// <summary>
        /// Safe hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        public static int SequenceGetHashSafe<T>(this IEnumerable<T>? seq)
        {
            return SequenceGetHashSafe(seq, t => EqualityComparer<T>.Default.GetHashCode(t!));
        }

        /// <summary>
        /// Zip a collection with an enumerable that is shorter
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <param name="v1"></param>
        public static IEnumerable<(T1, T2)> Zip<T1, T2>(this IEnumerable<T1>? t1,
            IEnumerable<T2> t2, T2 v1)
        {
            return (t1 ?? Enumerable.Empty<T1>()).Zip(t2.ContinueWith(v1));
        }

        /// <summary>
        /// Zip a collection with 2 enumerables that are shorter
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <param name="v2"></param>
        /// <param name="t3"></param>
        /// <param name="v3"></param>
        public static IEnumerable<(T1, T2, T3)> Zip<T1, T2, T3>(this IEnumerable<T1>? t1,
            IEnumerable<T2> t2, T2 v2, IEnumerable<T3> t3, T3 v3)
        {
            return (t1 ?? Enumerable.Empty<T1>()).Zip(t2.ContinueWith(v2), t3.ContinueWith(v3));
        }

        /// <summary>
        /// Continue a sequence with infinitely returning value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="value"></param>
        public static IEnumerable<T> ContinueWith<T>(this IEnumerable<T>? seq, T value)
        {
            if (seq != null)
            {
                foreach (var item in seq)
                {
                    yield return item;
                }
            }
            while (true)
            {
                yield return value;
            }
        }

        /// <summary>
        /// Safe hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="hash"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static int SequenceGetHashSafe<T>(this IEnumerable<T>? seq, Func<T, int> hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashCode = -932366343;
            if (seq != null)
            {
                foreach (var item in seq)
                {
                    hashCode = (hashCode * -1521134295) + hash(item);
                }
            }
            return hashCode;
        }

        /// <summary>
        /// Safe sequence equals
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="that"></param>
        public static bool SequenceEqualsSafe<T>(this IEnumerable<T>? seq,
            IEnumerable<T>? that)
        {
            if (seq == that)
            {
                return true;
            }
            if (seq == null || that == null)
            {
                if (!(that?.Any() ?? false))
                {
                    return !(seq?.Any() ?? false);
                }
                return false;
            }
            return seq.SequenceEqual(that);
        }

        /// <summary>
        /// Safe sequence equals
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="that"></param>
        /// <param name="func"></param>
        public static bool SequenceEqualsSafe<T>(this IEnumerable<T>? seq,
            IEnumerable<T>? that, Func<T?, T?, bool> func)
        {
            if (seq == that)
            {
                return true;
            }
            if (seq == null || that == null)
            {
                if (!(that?.Any() ?? false))
                {
                    return !(seq?.Any() ?? false);
                }
                return false;
            }
            return seq.SequenceEqual(that, Compare.Using(func));
        }

        /// <summary>
        /// Safe set equals
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="that"></param>
        /// <param name="func"></param>
        public static bool SetEqualsSafe<T>(this IEnumerable<T>? seq, IEnumerable<T>? that,
            Func<T?, T?, bool> func)
        {
            if (seq == that)
            {
                return true;
            }
            if (seq == null || that == null)
            {
                return false;
            }
            var source = new HashSet<T>(seq, Compare.Using(func));
            return source.SetEquals(that);
        }

        /// <summary>
        /// Safe set equals
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="that"></param>
        public static bool SetEqualsSafe<T>(this IEnumerable<T>? seq, IEnumerable<T>? that)
        {
            if (seq == that)
            {
                return true;
            }
            if (seq == null || that == null)
            {
                return false;
            }
            if (seq is ISet<T> setx)
            {
                return setx.SetEquals(that);
            }
            if (that is ISet<T> sety)
            {
                return sety.SetEquals(seq);
            }
            return new HashSet<T>(seq).SetEquals(that);
        }

        /// <summary>
        /// Merge enumerable b into set a.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        public static HashSet<T> MergeWith<T>(this IEnumerable<T>? a, IEnumerable<T>? b)
        {
            HashSet<T>? result = null;
            if (b?.Any() == true)
            {
                if (a == null)
                {
                    result = b.ToHashSet();
                }
                else
                {
                    result = new HashSet<T>(a);
                    foreach (var item in b)
                    {
                        result.Add(item);
                    }
                }
            }
            return result ?? new HashSet<T>();
        }
    }
}
