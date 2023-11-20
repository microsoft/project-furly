// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Queryable wrapper
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class DocumentQuery<T> : IQuery<T>
    {
        /// <summary>
        /// Create query from queryable
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="serializer"></param>
        /// <param name="ordered"></param>
        /// <param name="logger"></param>
        internal DocumentQuery(IQueryable<T> queryable, ISerializer serializer,
            bool ordered, ILogger logger)
        {
            _queryable = queryable;
            _serializer = serializer;
            _logger = logger;
            _ordered = ordered;
        }

        /// <inheritdoc/>
        public IResultFeed<IDocumentInfo<T>> GetResults()
        {
            return new DocumentInfoFeed<T>(_queryable.ToStreamIterator(),
                _serializer, _logger);
        }

        /// <inheritdoc/>
        public async Task<int> CountAsync(CancellationToken ct)
        {
            return await _queryable.CountAsync(ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public IQuery<T> OrderBy<K>(Expression<Func<T, K>> keySelector)
        {
            return new DocumentQuery<T>(_queryable.OrderBy(keySelector),
                _serializer, true, _logger);
        }

        /// <inheritdoc/>
        public IQuery<T> OrderByDescending<K>(Expression<Func<T, K>> keySelector)
        {
            return new DocumentQuery<T>(_queryable.OrderByDescending(keySelector),
                _serializer, true, _logger);
        }

        /// <inheritdoc/>
        public IQuery<K> Select<K>(Expression<Func<T, K>> selector)
        {
            return new DocumentQuery<K>(_queryable.Select(selector),
                _serializer, _ordered, _logger);
        }

        /// <inheritdoc/>
        public IQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            return new DocumentQuery<T>(_queryable.Where(predicate),
                _serializer, _ordered, _logger);
        }

        /// <inheritdoc/>
        public IQuery<K> SelectMany<K>(Expression<Func<T, IEnumerable<K>>> selector)
        {
            return new DocumentQuery<K>(_queryable.SelectMany(selector),
                _serializer, _ordered, _logger);
        }

        /// <inheritdoc/>
        public IQuery<T> Take(int maxRecords)
        {
            return new DocumentQuery<T>(_queryable.Take(maxRecords),
                _serializer, _ordered, _logger);
        }

        /// <inheritdoc/>
        public IQuery<T> Skip(int records)
        {
            return new DocumentQuery<T>(_queryable.Skip(records),
                _serializer, _ordered, _logger);
        }

        /// <inheritdoc/>
        public IQuery<T> Distinct()
        {
            var queryable = _queryable;
            if (!_ordered)
            {
                queryable = queryable.OrderBy(x => x);
            }
            return new DocumentQuery<T>(queryable.Distinct(),
                _serializer, _ordered, _logger);
        }

        private readonly IQueryable<T> _queryable;
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;
        private readonly bool _ordered;
    }
}
