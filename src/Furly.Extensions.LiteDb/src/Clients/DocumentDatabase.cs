// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb.Clients
{
    using Furly.Extensions.Storage;
    using LiteDB;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Lite database
    /// </summary>
    internal sealed class DocumentDatabase : IDatabase, IDisposable
    {
        /// <summary>
        /// Creates database
        /// </summary>
        /// <param name="db"></param>
        internal DocumentDatabase(ILiteDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        public Task<IDocumentCollection> OpenContainerAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            id = id.Replace('-', '_');
            var container = new DocumentCollection(id, _db);
            return Task.FromResult<IDocumentCollection>(container);
        }

        /// <inheritdoc/>
        public Task DeleteContainerAsync(string? id)
        {
            _db.DropCollection(id);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _db.Dispose();
        }

        private readonly ILiteDatabase _db;
    }
}
