// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Exceptions;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Extensions.Logging;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps a cosmos db container
    /// </summary>
    internal sealed class DocumentCollection : IDocumentCollection
    {
        /// <inheritdoc/>
        public string Name => _container.Id;

        /// <summary>
        /// Create collection
        /// </summary>
        /// <param name="container"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        internal DocumentCollection(Container container, ISerializer serializer, ILogger logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc/>
        public IQuery<T> CreateQuery<T>(int? pageSize)
        {
            if (pageSize < 1)
            {
                pageSize = null;
            }
            var query = _container.GetItemLinqQueryable<T>(false, null,
                new QueryRequestOptions
                {
                    MaxItemCount = pageSize,
                    // ConsistencyLevel,
                    // PartitionKey =
                    //  EnableScanInQuery = true
                });
            return new DocumentQuery<T>(query, _serializer, false, _logger);
        }

        /// <inheritdoc/>
        public IResultFeed<IDocumentInfo<T>> ContinueQuery<T>(string continuationToken,
            int? pageSize, string? partitionKey)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }
            if (!continuationToken.Contains("\"Continuation\":", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Not a valid continuation token.",
                    nameof(continuationToken));
            }
            if (pageSize < 1)
            {
                pageSize = null;
            }
            var query = _container.GetItemLinqQueryable<T>(false, continuationToken,
                new QueryRequestOptions
                {
                    MaxItemCount = pageSize,
                    // ConsistencyLevel,
                    // PartitionKey = partitionKey
                    //  EnableScanInQuery = true
                });
            return new DocumentInfoFeed<T>(query.ToStreamIterator(), _serializer, _logger);
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
                return await ExponentialBackoff.RetryAsync(_logger, async () =>
                {
                    try
                    {
                        var doc = await _container.ReadItemStreamAsync(id,
                            partitionKey: PartitionKey.None /*TODO*/, null, ct).ConfigureAwait(false);
                        doc.EnsureSuccessStatusCode();
                        return AsDocumentInfo<T>(doc.Content);
                    }
                    catch (Exception ex)
                    {
                        throw FilterException(ex);
                    }
                }, ct).ConfigureAwait(false);
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>> UpsertAsync<T>(T newItem,
            string? id, string? etag, CancellationToken ct)
        {
            if (newItem == null)
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            return await ExponentialBackoff.RetryAsync(_logger, async () =>
            {
                try
                {
                    var doc = await _container.UpsertItemStreamAsync(AsStream(newItem, id),
                        partitionKey: PartitionKey.None /*TODO*/,
                        new ItemRequestOptions
                        {
                            IfMatchEtag = etag,
                            EnableContentResponseOnWrite = true
                        }, ct).ConfigureAwait(false);
                    doc.EnsureSuccessStatusCode();
                    return AsDocumentInfo<T>(doc.Content);
                }
                catch (Exception ex)
                {
                    throw FilterException(ex);
                }
            }, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>> ReplaceAsync<T>(IDocumentInfo<T> existing,
            T newItem, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(existing);
            if (string.IsNullOrEmpty(existing.Id))
            {
                throw new ArgumentException("Missing id", nameof(existing));
            }
            if (newItem == null)
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            return await ExponentialBackoff.RetryAsync(_logger, async () =>
            {
                try
                {
                    var doc = await _container.ReplaceItemStreamAsync(
                        AsStream(newItem, existing.Id), existing.Id,
                        partitionKey: PartitionKey.None /*TODO*/,
                        new ItemRequestOptions
                        {
                            IfMatchEtag = existing.Etag,
                            EnableContentResponseOnWrite = true
                        }, ct).ConfigureAwait(false);
                    doc.EnsureSuccessStatusCode();
                    return AsDocumentInfo<T>(doc.Content);
                }
                catch (Exception ex)
                {
                    throw FilterException(ex);
                }
            }, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IDocumentInfo<T>> AddAsync<T>(T newItem, string? id,
            CancellationToken ct)
        {
            if (newItem == null)
            {
                throw new ArgumentNullException(nameof(newItem));
            }
            return await ExponentialBackoff.RetryAsync(_logger, async () =>
            {
                try
                {
                    var doc = await _container.CreateItemStreamAsync(AsStream(newItem, id),
                        partitionKey: PartitionKey.None /*TODO*/,
                        new ItemRequestOptions
                        {
                            EnableContentResponseOnWrite = true
                        }, ct).ConfigureAwait(false);
                    doc.EnsureSuccessStatusCode();
                    return new DocumentInfo<T>(_serializer.Parse(ReadAsBuffer(doc.Content)));
                }
                catch (Exception ex)
                {
                    throw FilterException(ex);
                }
            }, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task DeleteAsync<T>(IDocumentInfo<T> item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            return DeleteAsync<T>(item.Id, item.Etag, ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync<T>(string id, string? etag, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            await ExponentialBackoff.RetryAsync(_logger, async () =>
            {
                try
                {
                    var doc = await _container.DeleteItemStreamAsync(id,
                        partitionKey: PartitionKey.None /*TODO*/,
                        new ItemRequestOptions { IfMatchEtag = etag, }, ct).ConfigureAwait(false);
                    doc.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    throw FilterException(ex);
                }
            }, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Convert to document info
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        private DocumentInfo<T> AsDocumentInfo<T>(Stream stream)
        {
            return new DocumentInfo<T>(_serializer.Parse(ReadAsBuffer(stream)));
        }

        /// <summary>
        /// Convert to stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private MemoryStream AsStream<T>(T item, string? id = null)
        {
            var newDoc = new DocumentInfo<T>(_serializer.FromObject(item), id).Document;
            return new MemoryStream(_serializer.SerializeObjectToMemory(newDoc).ToArray());
        }

        /// <summary>
        /// Helper extension to convert an entire stream into a buffer...
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        internal static ArraySegment<byte> ReadAsBuffer(Stream stream)
        {
            // Try to read as much as possible
            var body = new byte[1024];
            var offset = 0;
            try
            {
                while (true)
                {
                    var read = stream.Read(body, offset, body.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                    if (offset == body.Length)
                    {
                        // Grow
                        var newbuf = new byte[body.Length * 2];
                        Buffer.BlockCopy(body, 0, newbuf, 0, body.Length);
                        body = newbuf;
                    }
                }
            }
            catch (IOException) { }
            return new ArraySegment<byte>(body, 0, offset);
        }

        /// <summary>
        /// Filter exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        internal static Exception FilterException(Exception ex)
        {
            switch (ex)
            {
                case HttpRequestException re when re.StatusCode != null:
                    return Wrap(re.StatusCode.Value, re.Message, re);
                case CosmosException dce:
                    if (dce.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        return new TemporarilyBusyException(dce.Message,
                            dce, dce.RetryAfter);
                    }
                    return Wrap(dce.StatusCode, dce.Message, dce);
            }
            return ex;

            static Exception Wrap(HttpStatusCode statusCode, string message,
                Exception inner)
            {
                switch (statusCode)
                {
                    case HttpStatusCode.MethodNotAllowed:
                        return new InvalidOperationException(message, inner);
                    case HttpStatusCode.NotAcceptable:
                    case HttpStatusCode.BadRequest:
                        return new BadRequestException(message, inner);
                    case HttpStatusCode.Forbidden:
                        return new ResourceInvalidStateException(message, inner);
                    case HttpStatusCode.Unauthorized:
                        return new UnauthorizedAccessException(message, inner);
                    case HttpStatusCode.NotFound:
                        return new ResourceNotFoundException(message);
                    case HttpStatusCode.Conflict:
                        return new ResourceConflictException(message, inner);
                    case HttpStatusCode.RequestTimeout:
                        return new TimeoutException(message, inner);
                    case HttpStatusCode.PreconditionFailed:
                        return new ResourceOutOfDateException(message, inner);
                    case HttpStatusCode.InternalServerError:
                        return new ResourceInvalidStateException(message, inner);
                    case HttpStatusCode.GatewayTimeout:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.TemporaryRedirect:
                    case HttpStatusCode.TooManyRequests:
                        return new TemporarilyBusyException(message, inner);
                }
                return inner;
            }
        }

        private readonly ILogger _logger;
        private readonly Container _container;
        private readonly ISerializer _serializer;
    }
}
