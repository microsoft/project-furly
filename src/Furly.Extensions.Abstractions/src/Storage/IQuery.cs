// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lightweight queryable abstraction
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IQuery<T>
    {
        /// <summary>
        /// Where predicate
        /// </summary>
        /// <param name="predicate"></param>
        IQuery<T> Where(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Order
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="keySelector"></param>
        IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);

        /// <summary>
        /// Order
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="keySelector"></param>
        IQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);

        /// <summary>
        /// Project
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="selector"></param>
#pragma warning disable CA1716 // Identifiers should not match keywords
        IQuery<TKey> Select<TKey>(Expression<Func<T, TKey>> selector);
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// Project many
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="selector"></param>
        IQuery<TKey> SelectMany<TKey>(Expression<Func<T, IEnumerable<TKey>>> selector);

        /// <summary>
        /// Limit to max documents to return
        /// </summary>
        /// <param name="maxDocuments"></param>
        IQuery<T> Take(int maxDocuments = 1);

        /// <summary>
        /// Filter duplicates
        /// </summary>
        IQuery<T> Distinct();

        /// <summary>
        /// Run query and return feed
        /// </summary>
        IResultFeed<IDocumentInfo<T>> GetResults();

        /// <summary>
        /// Count
        /// </summary>
        /// <param name="ct"></param>
        Task<int> CountAsync(CancellationToken ct = default);
    }
}
