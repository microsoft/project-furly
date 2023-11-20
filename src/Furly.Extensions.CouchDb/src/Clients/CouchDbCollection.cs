// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.Storage;
    using Furly.Exceptions;
    using CouchDB.Driver;
    using CouchDB.Driver.Exceptions;
    using CouchDB.Driver.Query;
    using Flurl.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps a collection
    /// </summary>
    internal sealed class CouchDbCollection : IDocumentCollection
    {
        /// <inheritdoc/>
        public string Name { get; }

        /// <summary>
        /// Create container
        /// </summary>
        /// <param name="name"></param>
        /// <param name="db"></param>
        /// <param name="logger"></param>
        internal CouchDbCollection(string name, ICouchDatabase<CouchDbDocument> db,
            ILogger logger)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>?> FindAsync<T>(string id, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            try
            {
                var doc = await _db.FindAsync(id, cancellationToken: ct).ConfigureAwait(false);
                return doc?.ToDocumentInfo<T>();
            }
            catch (Exception ex)
            {
                FilterException(ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>> UpsertAsync<T>(T newItem,
            string? id, string? etag, CancellationToken ct)
        {
            if (EqualityComparer<T?>.Default.Equals(newItem, default))
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            if (typeof(T).IsValueType)
            {
                throw new NotSupportedException(typeof(T).Name);
            }
            while (true)
            {
                var newDoc = CouchDbDocument.Wrap(newItem, id, etag);
                var force = string.IsNullOrEmpty(newDoc.Rev);
                try
                {
                    if (force)
                    {
                        // Retrieve top etag or null if not found
                        newDoc.Rev = await GetRevAsync(newDoc.Id, ct).ConfigureAwait(false);
                    }
                    return await AddOrUpdateAsync<T>(newDoc, ct).ConfigureAwait(false);
                }
                catch (ResourceConflictException) when (force) { }
                catch (ResourceNotFoundException) when (force) { }
                catch (ResourceOutOfDateException) when (!force)
                {
                    etag = await GetRevAsync(newDoc.Id, ct).ConfigureAwait(false);
                    if (etag != null)
                    {
                        throw;
                    }
                    etag = null;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>> ReplaceAsync<T>(IDocumentInfo<T> existing,
            T newItem, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(existing);
            if (EqualityComparer<T?>.Default.Equals(newItem, default))
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            if (string.IsNullOrEmpty(existing.Id))
            {
                throw new ArgumentException("Missing id", nameof(existing));
            }
            if (string.IsNullOrEmpty(existing.Etag))
            {
                throw new ArgumentException("Missing etag", nameof(existing));
            }
            if (typeof(T).IsValueType)
            {
                throw new NotSupportedException(typeof(T).Name);
            }
            var newDoc = CouchDbDocument.Wrap(newItem, existing.Id, existing.Etag);
            try
            {
                return await AddOrUpdateAsync<T>(newDoc, ct).ConfigureAwait(false);
            }
            catch (ResourceOutOfDateException e)
            {
                var rev = await GetRevAsync(existing.Id, ct).ConfigureAwait(false);
                if (rev == null)
                {
                    // Existing item is deleted so update exception
                    throw new ResourceNotFoundException(e.Message, e);
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public Task<IDocumentInfo<T>> AddAsync<T>(T newItem, string? id,
            CancellationToken ct)
        {
            if (EqualityComparer<T?>.Default.Equals(newItem, default))
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            if (typeof(T).IsValueType)
            {
                throw new NotSupportedException(typeof(T).Name);
            }
            var newDoc = CouchDbDocument.Wrap(newItem, id, null);
            return AddOrUpdateAsync<T>(newDoc, ct);
        }

        /// <inheritdoc/>
        public Task DeleteAsync<T>(IDocumentInfo<T> item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (string.IsNullOrEmpty(item.Id))
            {
                throw new ArgumentException("Id is missing", nameof(item));
            }
            if (string.IsNullOrEmpty(item.Etag))
            {
                throw new ArgumentException("Etag is missing", nameof(item));
            }
            if (typeof(T).IsValueType)
            {
                throw new NotSupportedException(typeof(T).Name);
            }
            return DeleteAsync<T>(item.Id, item.Etag, ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync<T>(string id, string? etag, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (typeof(T).IsValueType)
            {
                throw new NotSupportedException(typeof(T).Name);
            }
            var force = string.IsNullOrEmpty(etag);
            while (true)
            {
                try
                {
                    if (force)
                    {
                        // Retrieve top etag or null if not found
                        etag = await GetRevAsync(id, ct).ConfigureAwait(false);
                    }
                    try
                    {
                        await DeleteAsync(id, etag!, ct).ConfigureAwait(false);
                        return; // Success
                    }
                    catch (Exception ex)
                    {
                        FilterException(ex);
                        throw;
                    }
                }
                catch (ResourceConflictException) when (force) { }
                catch (ResourceNotFoundException) when (force) { }
            }
        }

        /// <inheritdoc/>
        public IQuery<T> CreateQuery<T>(int? pageSize)
        {
            return new ServerSideQuery<T>(this, Enumerable.Empty<T>().AsQueryable(),
                typeof(T), pageSize, null);
        }

        /// <inheritdoc/>
        public IResultFeed<IDocumentInfo<T>> ContinueQuery<T>(string continuationToken,
            int? pageSize, string? partitionKey)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }
            if (_queryStore.TryGetValue(continuationToken, out var feed))
            {
                if (feed is ICouchDbFeed<T> result)
                {
                    result.PageSize = pageSize;
                    return result;
                }
                throw new BadRequestException(
                    $"Continuation token: {continuationToken} type mismatch");
            }
            throw new BadRequestException($"Invalid continuation token: {continuationToken}.");
        }

        /// <summary>
        /// Helper to add or update
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newDoc"></param>
        /// <param name="ct"></param>
        internal async Task<IDocumentInfo<T>> AddOrUpdateAsync<T>(
            CouchDbDocument newDoc, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(newDoc.Rev))
                {
                    // Insert
                    newDoc = await _db.AddAsync(newDoc, false, ct).ConfigureAwait(false);
                }
                else
                {
                    // Upsert
                    newDoc = await _db.AddOrUpdateAsync(newDoc, false, ct).ConfigureAwait(false);
                }
                return newDoc.ToDocumentInfo<T>();
            }
            catch (Exception ex)
            {
                FilterException(ex, string.IsNullOrEmpty(newDoc.Rev));
                throw;
            }
        }

        /// <summary>
        /// Remove item with id and revision
        /// </summary>
        /// <param name="id"></param>
        /// <param name="rev"></param>
        /// <param name="ct"></param>
        /// <exception cref="CouchDeleteException"></exception>
        internal async Task DeleteAsync(string id, string rev,
            CancellationToken ct = default)
        {
            var result = await _db.NewRequest()
                .AppendPathSegment(id)
                .SetQueryParam("rev", rev)
                .DeleteAsync(ct)
                .SendRequestAsync()
                .ReceiveJson<CouchDbDeleted>()
                .ConfigureAwait(false);
            if (!result.Ok)
            {
                throw new CouchDeleteException();
            }
        }

        /// <summary>
        /// Delete Result
        /// </summary>
        internal class CouchDbDeleted
        {
            /// <summary> Ok </summary>
            [JsonProperty("ok")]
            public bool Ok { get; set; }
        }

        /// <summary>
        /// Query with continuation
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="mango"></param>
        /// <param name="pageSize"></param>
        /// <param name="bookmark"></param>
        /// <param name="ct"></param>
        /// <exception cref="FormatException"></exception>
        internal async Task<QueryResults> QueryAsync(string typeName,
            string mango, int? pageSize = null, string? bookmark = null,
            CancellationToken ct = default)
        {
            try
            {
                var o = JObject.Parse(mango);
                if (!string.IsNullOrEmpty(bookmark))
                {
                    o["bookmark"] = bookmark;
                }
                o["limit"] = pageSize ?? kMinQueryResultSize;
                // Get sort fields and ensure indexes were created
                if (o.TryGetValue("sort", out var fields))
                {
                    var indexes = fields
                        .Select(f => f is JObject o ?
                            o.Properties().FirstOrDefault()?.Name : (string?)f)
                        .Where(f => f != null)
                        .Select(f => GetOrAddIndexAsync(typeName, f!));
                    var indexresults = await Task.WhenAll(
                        indexes).ConfigureAwait(false);
                    o["use_index"] = JToken.FromObject(
                        indexresults.Select(i => i.Name).ToArray());
                }
                mango = o.ToString();
            }
            catch (JsonException)
            {
                throw new FormatException("Mango query is not Json");
            }
            _logger.LogDebug("Sending query {Mango}", mango);
            return await _db.NewRequest()
                .AppendPathSegments("_find")
                .WithHeader("Content-Type", ContentMimeType.Json)
                .PostStringAsync(mango, ct)
                .SendRequestAsync(mango)
                .ReceiveJson<QueryResults>()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Query results
        /// </summary>
        internal class QueryResults
        {
            /// <summary> Docs </summary>
            [JsonProperty("docs")]
            public List<CouchDbDocument>? Docs { get; internal set; }

            /// <summary> Bookmark </summary>
            [JsonProperty("bookmark")]
            public string? Bookmark { get; internal set; }
        }

        /// <summary>
        /// Get revision of document
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ct"></param>
        internal async Task<string?> GetRevAsync(string id,
            CancellationToken ct = default)
        {
            while (true)
            {
                try
                {
                    using var result = await _db.NewRequest()
                        .AppendPathSegment(id)
                        .AllowAnyHttpStatus()
                        .HeadAsync(ct, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false);

                    if (result.IsSuccessful() &&
                        result.ResponseMessage.Headers.TryGetValues("ETag", out var values))
                    {
                        return values?.FirstOrDefault()?.TrimQuotes();
                    }
                    return null;
                }
                catch (FlurlHttpException ex)
                {
                    var e = await ex.TranslateExceptionAsync().ConfigureAwait(false);
                    _logger.LogError(e, "Failure when trying to get revision");
                }
            }
        }

        /// <summary>
        /// Ensure an index for this field in type exists
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="field"></param>
        /// <param name="ct"></param>
        internal Task<IndexResult> GetOrAddIndexAsync(string typeName, string field,
            CancellationToken ct = default)
        {
            var index = typeName + "_" + field.Replace('.', '_');
            if (field == "_id")
            {
                index = field;
            }
            return _cache.GetOrAdd(index, async name =>
            {
                return await _db.NewRequest()
                    .AppendPathSegments("_index")
                    .PostJsonAsync(new
                    {
                        name,
                        index = new
                        {
                            fields = new[] { field }
                        },
                        type = "json"
                    }, ct)
                    .SendRequestAsync()
                    .ReceiveJson<IndexResult>().ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Index result
        /// </summary>
        internal class IndexResult
        {
            /// <summary> Name </summary>
            [JsonProperty("name")]
            public string? Name { get; internal set; }
        }

        /// <summary>
        /// Filter exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="isAdd"></param>
        /// <exception cref="ResourceNotFoundException"></exception>
        /// <exception cref="ResourceConflictException"></exception>
        /// <exception cref="ResourceOutOfDateException"></exception>
        /// <exception cref="ResourceInvalidStateException"></exception>
        /// <exception cref="ExternalDependencyException"></exception>
        internal static void FilterException(Exception ex, bool isAdd = false)
        {
            switch (ex)
            {
                case CouchNoIndexException ni:
                    throw new ResourceNotFoundException(ni.Message, ni);
                case CouchNotFoundException nf:
                    throw new ResourceNotFoundException(nf.Message, nf);
                case CouchConflictException ce:
                    if (isAdd)
                    {
                        throw new ResourceConflictException(ce.Message, ce);
                    }
                    throw new ResourceOutOfDateException(ce.Message, ce);
                case CouchDeleteException de:
                    throw new ResourceInvalidStateException(de.Message, de);
                case CouchException cc:
                    if (cc.Reason == "Invalid rev format")
                    {
                        throw new ResourceOutOfDateException(cc.Message, cc);
                    }
                    throw new ExternalDependencyException(cc.Message, cc);
            }
        }

        /// <summary>
        /// Result feed
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TServer"></typeparam>
        internal sealed class FilteredResultFeed<TResult, TServer> : ICouchDbFeed<TResult>
        {
            /// <inheritdoc/>
            public string? ContinuationToken
            {
                get
                {
                    lock (_lock)
                    {
                        if (_items.Count != 0)
                        {
                            return _continuationToken;
                        }
                        return _server.ContinuationToken;
                    }
                }
            }

            /// <inheritdoc/>
            public int? PageSize { get; set; }

            /// <summary>
            /// Create feed
            /// </summary>
            /// <param name="collection"></param>
            /// <param name="server"></param>
            /// <param name="pageSize"></param>
            /// <param name="filter"></param>
            internal FilteredResultFeed(CouchDbCollection collection,
                ServerResultFeed<TServer> server, int? pageSize, IQueryable<TResult> filter)
            {
                _server = server ?? throw new ArgumentNullException(nameof(server));
                _collection = collection ?? throw new ArgumentNullException(nameof(collection));
                _items = new Queue<IDocumentInfo<TResult>>();
                _continuationToken = Guid.NewGuid().ToString();
                _collection._queryStore.Add(_continuationToken, this);
                PageSize = pageSize;
                _filter = filter;
            }

            /// <inheritdoc/>
            public bool HasMore()
            {
                lock (_lock)
                {
                    if (_items.Count == 0 && !_server.HasMore())
                    {
                        _collection._queryStore.Remove(_continuationToken);
                        return false;
                    }
                    return true;
                }
            }

            /// <inheritdoc/>
            public async Task<IEnumerable<IDocumentInfo<TResult>>> ReadAsync(CancellationToken ct)
            {
                if (_server.HasMore())
                {
                    var results = new List<IDocumentInfo<TServer>>();
                    while (_server.HasMore())
                    { // Read all from server
                        var serverResults = await _server.ReadAsync(ct).ConfigureAwait(false);
                        results.AddRange(serverResults);
                    }

                    foreach (var item in ProcessServerResults(results, _filter))
                    {
                        _items.Enqueue(item);
                    }
                }
                return Read();
            }

            /// <summary>
            /// Internal read
            /// </summary>
            internal IEnumerable<IDocumentInfo<TResult>> Read()
            {
                lock (_lock)
                {
                    var page = new List<IDocumentInfo<TResult>>(PageSize ?? _items.Count);
                    for (var i = 0; (!PageSize.HasValue || i < PageSize.Value) &&
                        _items.Count != 0; i++)
                    {
                        page.Add(_items.Dequeue());
                    }
                    if (_items.Count == 0)
                    {
                        _collection._queryStore.Remove(_continuationToken);
                    }
                    return page;
                }
            }

            /// <summary>
            /// Apply filter
            /// </summary>
            /// <param name="results"></param>
            /// <param name="filter"></param>
            private static IEnumerable<IDocumentInfo<TResult>> ProcessServerResults(
                IEnumerable<IDocumentInfo<TServer>> results, IQueryable<TResult> filter)
            {
                var serverResults = results
                    .Select(d => d.Value)
                    .AsQueryable();
                // TODO - change expression to replace type with wrapper
                var expr = new UpdateExpressionTarget(serverResults).Visit(filter.Expression);
                var filteredResult = serverResults.Provider.CreateQuery<TResult>(expr);
                return filteredResult.AsEnumerable()
                    .Select(item => CouchDbDocument.Wrap(item, null, null))
                    .Select(d => d.ToDocumentInfo<TResult>());
            }

            /// <summary>
            /// Changes the target of the expression
            /// </summary>
            private class UpdateExpressionTarget : ExpressionVisitor
            {
                private readonly IQueryable<TServer> _results;

                /// <inheritdoc/>
                internal UpdateExpressionTarget(IQueryable<TServer> results)
                {
                    _results = results;
                }

                /// <inheritdoc/>
                protected override Expression VisitConstant(ConstantExpression node)
                {
                    return node.Type == typeof(EnumerableQuery<TServer>) ?
                        Expression.Constant(_results) : node;
                }
            }

            private readonly object _lock = new();
            private readonly ServerResultFeed<TServer> _server;
            private readonly CouchDbCollection _collection;
            private readonly string _continuationToken;
            private readonly IQueryable<TResult> _filter;
            private readonly Queue<IDocumentInfo<TResult>> _items;
        }

        /// <summary>
        /// Result feed
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        internal sealed class ServerResultFeed<TResult> : ICouchDbFeed<TResult>
        {
            /// <inheritdoc/>
            public string? ContinuationToken
            {
                get
                {
                    lock (_lock)
                    {
                        if (string.IsNullOrEmpty(_bookmark))
                        {
                            return null;
                        }
                        return _bookmark;
                    }
                }
            }

            /// <inheritdoc/>
            public int? PageSize { get; set; }

            /// <summary>
            /// Create feed
            /// </summary>
            /// <param name="collection"></param>
            /// <param name="mango"></param>
            /// <param name="originalType"></param>
            /// <param name="pageSize"></param>
            /// <param name="limit"></param>
            internal ServerResultFeed(CouchDbCollection collection, string mango,
                string originalType, int? pageSize, int? limit)
            {
                _collection = collection ?? throw new ArgumentNullException(nameof(collection));
                _mango = mango ?? throw new ArgumentNullException(nameof(mango));
                _originalType = originalType;
                if (pageSize.HasValue && limit.HasValue && limit.Value <= pageSize.Value)
                {
                    // limit set is less than page size - no need to limit
                    pageSize = null;
                }
                PageSize = pageSize;
                _limit = limit ?? int.MaxValue;
                _bookmark = string.Empty; // First time query - fresh
            }

            /// <inheritdoc/>
            public bool HasMore()
            {
                lock (_lock)
                {
                    return _bookmark != null;
                }
            }

            /// <inheritdoc/>
            public async Task<IEnumerable<IDocumentInfo<TResult>>> ReadAsync(CancellationToken ct)
            {
                var docs = Enumerable.Empty<IDocumentInfo<TResult>>();
                if (!HasMore())
                { // exhausted - do not start query again
                    return docs;
                }

                // Query server
                var results = await _collection.QueryAsync(_originalType, _mango, PageSize,
                    string.IsNullOrEmpty(_bookmark) ? null : _bookmark, ct).ConfigureAwait(false);

                lock (_lock)
                {
                    var bookmark = results.Bookmark;
                    if (results.Docs == null || results.Docs.Count == 0)
                    {
                        bookmark = null; // Done
                    }
                    else
                    {
                        var take = Math.Min(results.Docs.Count, _limit);
                        _limit -= take;
                        if (take < results.Docs.Count)
                        {
                            bookmark = null; // Done
                            docs = results.Docs.Take(take)
                                .Select(d => d.ToDocumentInfo<TResult>());
                        }
                        else
                        {
                            docs = results.Docs
                                .Select(d => d.ToDocumentInfo<TResult>());
                        }
                        if (_limit == 0)
                        {
                            // Done
                            bookmark = null;
                        }
                    }

                    if (!string.IsNullOrEmpty(_bookmark))
                    {
                        // Remove old bookmark
                        _collection._queryStore.Remove(_bookmark);
                    }
                    _bookmark = bookmark;
                    if (!string.IsNullOrEmpty(_bookmark))
                    {
                        // Add new bookmark
                        _collection._queryStore.Add(_bookmark, this);
                    }
                    return docs;
                }
            }

            private readonly object _lock = new();
            private readonly CouchDbCollection _collection;
            private readonly string _originalType;
            private readonly string _mango;
            private string? _bookmark;
            private int _limit;
        }

        /// <summary>
        /// Client side
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TServer"></typeparam>
        internal sealed class ClientSideFilter<TResult, TServer> : IQuery<TResult>
        {
            /// <summary>
            /// Create query
            /// </summary>
            /// <param name="collection"></param>
            /// <param name="server"></param>
            /// <param name="filter"></param>
            /// <param name="pageSize"></param>
            internal ClientSideFilter(CouchDbCollection collection,
                ServerResultFeed<TServer> server, IQueryable<TResult> filter, int? pageSize)
            {
                _collection = collection ?? throw new ArgumentNullException(nameof(collection));
                _filter = filter ?? throw new ArgumentNullException(nameof(filter));
                _server = server ?? throw new ArgumentNullException(nameof(server));
                _pageSize = pageSize;
            }

            /// <inheritdoc/>
            public IResultFeed<IDocumentInfo<TResult>> GetResults()
            {
                return new FilteredResultFeed<TResult, TServer>(_collection,
                    _server, _pageSize, _filter);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Where(Expression<Func<TResult, bool>> predicate)
            {
                return new ClientSideFilter<TResult, TServer>(_collection,
                    _server, _filter.Where(predicate), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<TResult> OrderBy<K>(Expression<Func<TResult, K>> keySelector)
            {
                return new ClientSideFilter<TResult, TServer>(_collection,
                    _server, _filter.OrderBy(keySelector), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<TResult> OrderByDescending<K>(Expression<Func<TResult, K>> keySelector)
            {
                return new ClientSideFilter<TResult, TServer>(_collection,
                    _server, _filter.OrderByDescending(keySelector), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<K> Select<K>(Expression<Func<TResult, K>> selector)
            {
                return new ClientSideFilter<K, TServer>(_collection,
                    _server, _filter.Select(selector), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<K> SelectMany<K>(Expression<Func<TResult, IEnumerable<K>>> selector)
            {
                return new ClientSideFilter<K, TServer>(_collection,
                    _server, _filter.SelectMany(selector), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Take(int maxDocuments)
            {
                return new ClientSideFilter<TResult, TServer>(_collection,
                    _server, _filter.Take(maxDocuments), _pageSize);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Distinct()
            {
                return new ClientSideFilter<TResult, TServer>(_collection,
                    _server, _filter.Distinct(), _pageSize);
            }

            /// <inheritdoc/>
            public Task<int> CountAsync(CancellationToken ct = default)
            {
                var result = new FilteredResultFeed<TResult, TServer>(_collection,
                    _server, _pageSize, _filter);
                return result.CountAsync(ct);
            }

            private readonly IQueryable<TResult> _filter;
            private readonly ServerResultFeed<TServer> _server;
            private readonly CouchDbCollection _collection;
            private readonly int? _pageSize;
        }

        /// <summary>
        /// Server side
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        internal sealed class ServerSideQuery<TResult> : IQuery<TResult>
        {
            /// <summary>
            /// Create query
            /// </summary>
            /// <param name="collection"></param>
            /// <param name="queryable"></param>
            /// <param name="originalType"></param>
            /// <param name="pageSize"></param>
            /// <param name="limit"></param>
            internal ServerSideQuery(CouchDbCollection collection, IQueryable<TResult> queryable,
                Type originalType, int? pageSize, int? limit)
            {
                _queryable = queryable ?? throw new ArgumentNullException(nameof(queryable));
                _collection = collection ?? throw new ArgumentNullException(nameof(collection));
                _originalType = originalType;
                _limit = limit;
                _pageSize = pageSize;
            }

            /// <inheritdoc/>
            public IResultFeed<IDocumentInfo<TResult>> GetResults()
            {
                var mango = ExpressionToMango.Translate(_queryable.Expression);
                return new ServerResultFeed<TResult>(_collection, mango,
                    _originalType.Name, _pageSize, _limit);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Where(Expression<Func<TResult, bool>> predicate)
            {
                if (!ExpressionToMango.IsValid(predicate))
                {
                    return Complete().Where(predicate);
                }
                return new ServerSideQuery<TResult>(_collection,
                    _queryable.Where(predicate),
                    _originalType, _pageSize, _limit);
            }

            /// <inheritdoc/>
            public IQuery<TResult> OrderBy<K>(Expression<Func<TResult, K>> keySelector)
            {
                if (typeof(TResult) != _originalType // No index
                    || !ExpressionToMango.IsValid(keySelector))
                {
                    return Complete().OrderBy(keySelector);
                }
                return new ServerSideQuery<TResult>(_collection,
                    _queryable.OrderBy(keySelector),
                    _originalType, _pageSize, _limit);
            }

            /// <inheritdoc/>
            public IQuery<TResult> OrderByDescending<K>(Expression<Func<TResult, K>> keySelector)
            {
                if (typeof(TResult) != _originalType // No index
                    || !ExpressionToMango.IsValid(keySelector))
                {
                    return Complete().OrderByDescending(keySelector);
                }
                return new ServerSideQuery<TResult>(_collection,
                    _queryable.OrderByDescending(keySelector),
                    _originalType, _pageSize, _limit);
            }

            /// <inheritdoc/>
            public IQuery<K> Select<K>(Expression<Func<TResult, K>> selector)
            {
                if (typeof(K).IsValueType || typeof(K) == typeof(string) ||
                    !ExpressionToMango.IsValid(selector))
                {
                    return Complete().Select(selector);
                }
                return new ServerSideQuery<K>(_collection,
                    _queryable.Select(selector),
                    _originalType, _pageSize, _limit);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Take(int maxDocuments)
            {
                var queryable = _queryable;
                var pageSize = _pageSize;
                if (!pageSize.HasValue || pageSize.Value >= maxDocuments)
                {
                    queryable = queryable.Take(maxDocuments);
                    pageSize = null; // No paging necessary
                }
                return new ServerSideQuery<TResult>(_collection, queryable,
                    _originalType, pageSize, maxDocuments);
            }

            /// <inheritdoc/>
            public IQuery<TResult> Distinct()
            {
                return Complete().Distinct();
            }

            /// <inheritdoc/>
            public IQuery<K> SelectMany<K>(Expression<Func<TResult, IEnumerable<K>>> selector)
            {
                return Complete().SelectMany(selector);
            }

            /// <inheritdoc/>
            public Task<int> CountAsync(CancellationToken ct = default)
            {
                return Complete(true).CountAsync(ct);
            }

            /// <summary>
            /// Complete
            /// </summary>
            /// <param name="all"></param>
            private ClientSideFilter<TResult, TResult> Complete(bool all = false)
            {
                var mango = ExpressionToMango.Translate(_queryable.Expression);
                return new ClientSideFilter<TResult, TResult>(_collection,
                    new ServerResultFeed<TResult>(_collection, mango,
                        _originalType.Name, all ? null : _pageSize, all ? null : _limit),
                    Enumerable.Empty<TResult>().AsQueryable(), _pageSize);
            }

            private readonly Type _originalType;
            private readonly int? _limit;
            private readonly CouchDbCollection _collection;
            private readonly IQueryable<TResult> _queryable;
            private readonly int? _pageSize;
        }

        /// <summary>
        /// default query result limit is 25, make sure we return larger by default
        /// </summary>
        private const int kMinQueryResultSize = 200;
        private readonly ICouchDatabase<CouchDbDocument> _db;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, Task<IndexResult>> _cache =
            new();
        private readonly Dictionary<string, object> _queryStore =
            new();
    }
}
