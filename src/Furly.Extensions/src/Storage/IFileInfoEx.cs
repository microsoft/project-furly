// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Microsoft.Extensions.FileProviders;
    using System.IO;

    /// <summary>
    /// File info extensions
    /// </summary>
    public interface IFileInfoEx : IFileInfo
    {
        /// <summary>
        /// If the file is writable
        /// </summary>
        bool IsWritable { get; }

        /// <summary>
        /// Return file contents as writable stream.
        /// Caller should dispose stream when complete.
        /// </summary>
        /// <returns>The file stream</returns>
        Stream CreateWriteStream();
    }
}
