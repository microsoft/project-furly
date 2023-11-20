// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    /// <summary>
    /// Compare helper
    /// </summary>
    public static class Compare
    {
        /// <summary>
        /// Create equality comparer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        public static IEqualityComparer<T> Using<T>(Func<T?, T?, bool> func)
        {
            return new FuncCompare<T>(func);
        }
    }
}
