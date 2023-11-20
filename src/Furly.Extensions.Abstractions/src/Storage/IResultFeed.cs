// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// List of documents
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IResultFeed<T>
    {
        /// <summary>
        /// Get continuation token to continue read later
        /// </summary>
        string? ContinuationToken { get; }

        /// <summary>
        /// Returns whether there is more data in the feed
        /// </summary>
        bool HasMore();

        /// <summary>
        /// Read results from feed
        /// </summary>
        /// <param name="ct"></param>
        Task<IEnumerable<T>> ReadAsync(CancellationToken ct = default);
    }
}
