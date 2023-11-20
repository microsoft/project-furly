// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a database
    /// </summary>
    public interface IDatabase
    {
        /// <summary>
        /// Opens or creates a (default) collection as a
        /// collection of document elements.
        /// </summary>
        /// <param name="id"></param>
        Task<IDocumentCollection> OpenContainerAsync(
            string? id = null);

        /// <summary>
        /// Delete (default) collection in database
        /// </summary>
        /// <param name="id"></param>
        Task DeleteContainerAsync(string? id = null);
    }
}
