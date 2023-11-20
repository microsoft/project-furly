// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.Storage;
    using CouchDB.Driver;
    using CouchDB.Driver.Exceptions;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Lite database
    /// </summary>
    internal sealed class CouchDbDatabase : IDatabase, IDisposable
    {
        /// <summary>
        /// Creates database
        /// </summary>
        /// <param name="db"></param>
        /// <param name="logger"></param>
        internal CouchDbDatabase(CouchClient db, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        public async Task<IDocumentCollection> OpenContainerAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            while (true)
            {
                try
                {
                    var db = await _client.GetOrCreateDatabaseAsync<CouchDbDocument>(
                        id).ConfigureAwait(false);
                    return new CouchDbCollection(id, db, _logger);
                }
                catch (CouchException e)
                {
                    _logger.LogError(e, "Failure when trying to get or create database.");
                }
            }
        }

        /// <inheritdoc/>
        public Task DeleteContainerAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            return _client.DeleteDatabaseAsync(id);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private readonly CouchClient _client;
        private readonly ILogger _logger;
    }
}
