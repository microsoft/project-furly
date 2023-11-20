// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Furly.Exceptions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a collection of documents in a database
    /// </summary>
    public interface IDocumentCollection
    {
        /// <summary>
        /// Name of the collection
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Add new item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newItem"></param>
        /// <param name="id"></param>
        /// <param name="ct"></param>
        Task<IDocumentInfo<T>> AddAsync<T>(T newItem,
            string? id = null, CancellationToken ct = default);

        /// <summary>
        /// Finds an item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="ct"></param>
        Task<IDocumentInfo<T>?> FindAsync<T>(string id,
            CancellationToken ct = default);

        /// <summary>
        /// Replace item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="existing"></param>
        /// <param name="newItem"></param>
        /// <param name="ct"></param>
        Task<IDocumentInfo<T>> ReplaceAsync<T>(IDocumentInfo<T> existing,
            T newItem, CancellationToken ct = default);

        /// <summary>
        /// Adds or updates an item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="ResourceOutOfDateException"/>
        /// <param name="newItem"></param>
        /// <param name="id"></param>
        /// <param name="etag"></param>
        /// <param name="ct"></param>
        Task<IDocumentInfo<T>> UpsertAsync<T>(T newItem,
            string? id = null, string? etag = null,
            CancellationToken ct = default);

        /// <summary>
        /// Removes the item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="ResourceOutOfDateException"/>
        /// <param name="item"></param>
        /// <param name="ct"></param>
        Task DeleteAsync<T>(IDocumentInfo<T> item,
            CancellationToken ct = default);

        /// <summary>
        /// Delete an item by id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="etag"></param>
        /// <param name="ct"></param>
        Task DeleteAsync<T>(string id, string? etag = null,
            CancellationToken ct = default);

        /// <summary>
        /// Create Query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pageSize"></param>
        IQuery<T> CreateQuery<T>(int? pageSize = null);

        /// <summary>
        /// Continue a previously run query using continuation token
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="continuationToken"></param>
        /// <param name="pageSize"></param>
        /// <param name="partitionKey"></param>
        IResultFeed<IDocumentInfo<T>> ContinueQuery<T>(
            string continuationToken,
            int? pageSize = null, string? partitionKey = null);
    }
}
