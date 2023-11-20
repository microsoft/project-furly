// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using System.Threading.Tasks;

    /// <summary>
    /// Document database service
    /// </summary>
    public interface IDatabaseServer
    {
        /// <summary>
        /// Opens a named or default database
        /// </summary>
        /// <param name="id"></param>
        Task<IDatabase> OpenAsync(string? id = null);
    }
}
