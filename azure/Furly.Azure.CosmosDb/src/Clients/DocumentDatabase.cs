// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides document db database interface.
    /// </summary>
    internal sealed class DocumentDatabase : IDatabase
    {
        /// <summary>
        /// Creates database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        internal DocumentDatabase(Database database, IJsonSerializer serializer, ILogger logger)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _collections = new ConcurrentDictionary<string, DocumentCollection>();
        }

        /// <inheritdoc/>
        public async Task<IDocumentCollection> OpenContainerAsync(string? id)
        {
            return await OpenOrCreateCollectionAsync(id).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>> ListContainersAsync(CancellationToken ct)
        {
            var result = new List<string>();
            var resultSetIterator = _database.GetContainerQueryIterator<ContainerProperties>();
            while (resultSetIterator.HasMoreResults)
            {
                foreach (var container in await resultSetIterator.ReadNextAsync(ct).ConfigureAwait(false))
                {
                    result.Add(container.Id);
                }
            }
            return result;
        }

        /// <inheritdoc/>
        public async Task DeleteContainerAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            try
            {
                var container = _database.GetContainer(id);
                await container.DeleteContainerAsync().ConfigureAwait(false);
            }
            catch { }
            finally
            {
                _collections.TryRemove(id, out _);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _collections.Clear();
        }

        /// <summary>
        /// Create or Open collection
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task<DocumentCollection> OpenOrCreateCollectionAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            if (!_collections.TryGetValue(id, out var collection))
            {
                var container = await EnsureCollectionExistsAsync(id).ConfigureAwait(false);
                collection = _collections.GetOrAdd(id, k => new DocumentCollection(container,
                    _serializer, _logger));
            }
            return collection;
        }

        /// <summary>
        /// Ensures collection exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task<Container> EnsureCollectionExistsAsync(string? id)
        {
            var containerProperties = new ContainerProperties
            {
                Id = id,
                // DefaultTimeToLive = (int?)options?.ItemTimeToLive?.TotalMilliseconds ?? -1,
                PartitionKeyPath = "/_partitionKey",
                IndexingPolicy = new IndexingPolicy
                {
                    Automatic = true, // new RangeIndex(DataType.String) {
                                      //  Precision = -1
                                      //     })
                }
            };
            //  if (!string.IsNullOrEmpty(options?.PartitionKey)) {
            //      containerProperties.PartitionKeyPath = "/" + options.PartitionKey;
            //  }
            var container = await _database.CreateContainerIfNotExistsAsync(
                containerProperties).ConfigureAwait(false);

            return container.Container;
        }

        private readonly Database _database;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, DocumentCollection> _collections;
        private readonly IJsonSerializer _serializer;
    }
}
