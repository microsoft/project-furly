// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.IO;
    using System.IO.Compression;

    /// <summary>
    /// Byte buffer extensions
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Zip string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] Zip(this Stream input)
        {
            using (var result = new MemoryStream())
            {
                using (var gs = new GZipStream(result, CompressionMode.Compress))
                {
                    input.CopyTo(gs);
                }
                return result.ToArray();
            }
        }

        /// <summary>
        /// Unzip byte array
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] Unzip(this Stream input)
        {
            using (var output = new MemoryStream())
            {
                using (var gs = new GZipStream(input, CompressionMode.Decompress))
                {
                    gs.CopyTo(output);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Zip string to byte array
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] Zip(this byte[] str)
        {
            using (var input = new MemoryStream(str))
            {
                return input.Zip();
            }
        }

        /// <summary>
        /// Unzip from byte array to string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] Unzip(this byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            {
                return input.Unzip();
            }
        }
    }
}
