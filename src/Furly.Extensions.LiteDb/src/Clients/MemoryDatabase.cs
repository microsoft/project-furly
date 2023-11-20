// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb.Clients
{
    using Furly.Extensions.Storage;
    using LiteDB;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides in memory storage with litedb engine.
    /// </summary>
    public class MemoryDatabase : IDatabaseServer
    {
        /// <summary>
        /// Creates server
        /// </summary>
        public MemoryDatabase()
        {
        }

        /// <inheritdoc/>
        public virtual Task<IDatabase> OpenAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            id = id.Replace('-', '_').ToUpperInvariant();
            var client = _clients.GetOrAdd(id, _ => Open());
            var db = new DocumentDatabase(client);
            return Task.FromResult<IDatabase>(db);
        }

        /// <summary>
        /// Helper to create client
        /// </summary>
        private static LiteDatabase Open()
        {
            var client = new LiteDatabase(new MemoryStream(), DocumentSerializer.Mapper)
            {
                UtcDate = true
            };
            client.Rebuild(new LiteDB.Engine.RebuildOptions
            {
                Collation = new Collation(9, CompareOptions.Ordinal)
            });
            return client;
        }

        private readonly ConcurrentDictionary<string, LiteDatabase> _clients = new();
    }
}
