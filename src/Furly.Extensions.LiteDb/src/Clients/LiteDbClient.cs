// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb.Clients
{
    using Furly.Extensions.Storage;
    using LiteDB;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides document db and graph functionality for storage interfaces.
    /// </summary>
    public class LiteDbClient : MemoryDatabase
    {
        /// <summary>
        /// Creates server
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public LiteDbClient(IOptionsSnapshot<LiteDbOptions> options,
            ILogger<LiteDbClient> logger) : base()
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(_options.Value.DbConnectionString))
            {
                logger.LogWarning(
                    "No database connection string. Using in memory database!");
                logger.LogInformation(
                    "To persist your data, configure a connection string!");
            }
        }

        /// <inheritdoc/>
        public override Task<IDatabase> OpenAsync(string? id)
        {
            if (string.IsNullOrEmpty(_options.Value.DbConnectionString))
            {
                return base.OpenAsync(id);
            }
            var cs = new ConnectionString(_options.Value.DbConnectionString);
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            cs.Filename = (cs.Filename == null || cs.Filename.Trim(':') != cs.Filename ?
                id : Path.Combine(
                    Path.GetFullPath(cs.Filename), id)) + ".db";
            var client = new LiteDatabase(cs, DocumentSerializer.Mapper)
            {
                UtcDate = true
            };
            if (client.Collation.SortOptions != CompareOptions.Ordinal)
            {
                client.Rebuild(new LiteDB.Engine.RebuildOptions
                {
                    Collation = new Collation(9, CompareOptions.Ordinal)
                });
            }
            var db = new DocumentDatabase(client);
            return Task.FromResult<IDatabase>(db);
        }

        private readonly IOptionsSnapshot<LiteDbOptions> _options;
    }
}
