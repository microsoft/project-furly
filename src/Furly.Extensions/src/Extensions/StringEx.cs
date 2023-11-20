// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    /// <summary>
    /// String helper extensions
    /// </summary>
    public static class StringEx
    {
        /// <summary>
        /// Trims quotes
        /// </summary>
        /// <param name="value"></param>
        public static string TrimQuotes(this string value)
        {
            var trimmed = value.TrimMatchingChar('"');
            if (trimmed == value)
            {
                return value.TrimMatchingChar('\'');
            }
            return trimmed;
        }

        /// <summary>
        /// Trims a char from front and back if both match
        /// </summary>
        /// <param name="value"></param>
        /// <param name="match"></param>
        public static string TrimMatchingChar(this string value, char match)
        {
            if (value.Length >= 2 && value[0] == match &&
                value[^1] == match)
            {
                return value[1..^1];
            }
            return value;
        }
    }
}
