// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Microsoft.Extensions.FileProviders;

    /// <summary>
    /// File provider factory for a root folder
    /// </summary>
    public interface IFileProviderFactory
    {
        /// <summary>
        /// Create a file provider
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        IFileProvider Create(string root);
    }
}
