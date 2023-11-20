// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.Security.Cryptography;

    /// <summary>
    /// Byte buffer extensions
    /// </summary>
    public static class ByteArrayEx
    {
        /// <summary>
        /// Convert to base 16
        /// </summary>
        /// <param name="value"></param>
        /// <param name="upperCase"></param>
        public static string ToBase16String(this byte[] value,
            bool upperCase = true)
        {
            var charLookup = upperCase ?
                "0123456789ABCDEF" : "0123456789abcdef";
            var chars = new char[value.Length * 2];
            // no checking needed here
            var j = 0;
            for (var i = 0; i < value.Length; i++)
            {
                chars[j++] = charLookup[value[i] >> 4];
                chars[j++] = charLookup[value[i] & 0xf];
            }
            return new string(chars);
        }

        /// <summary>
        /// Convert to base 64
        /// </summary>
        /// <param name="value"></param>
        public static string ToBase64String(this byte[] value)
        {
            return Convert.ToBase64String(value);
        }

        /// <summary>
        /// Hashes the string
        /// </summary>
        /// <param name="bytestr">string to hash</param>
        public static string ToSha256Hash(this byte[] bytestr)
        {
            var hash = SHA256.HashData(bytestr);
            return hash.ToBase16String(false);
        }
    }
}
