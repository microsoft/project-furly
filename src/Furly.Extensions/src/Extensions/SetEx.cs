// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    using System.Linq;

    /// <summary>
    /// Set extensions
    /// </summary>
    public static class SetEx
    {
        /// <summary>
        /// Safe set equals
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <param name="that"></param>
        public static bool SetEqualsSafe<T>(this ISet<T>? seq, IEnumerable<T>? that)
        {
            if (seq == that)
            {
                return true;
            }
            if (seq == null || that == null)
            {
                if (!(that?.Any() ?? false))
                {
                    return (seq?.Count ?? 0) == 0;
                }
                return false;
            }
            return seq.SetEquals(that);
        }
    }
}
