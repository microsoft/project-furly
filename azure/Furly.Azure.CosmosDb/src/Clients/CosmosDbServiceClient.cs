// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Azure;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides document db and graph functionality for storage interfaces.
    /// </summary>
    public sealed class CosmosDbServiceClient : IDatabaseServer, IDisposable
    {
        /// <summary>
        /// Creates server
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public CosmosDbServiceClient(IOptions<CosmosDbOptions> options,
            IJsonSerializer serializer, ILogger<CosmosDbServiceClient> logger)
        {
            _options = options ??
                throw new ArgumentNullException(nameof(options));
            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));

            if (string.IsNullOrEmpty(_options.Value.ConnectionString))
            {
                throw new ArgumentException("Connection string missing", nameof(options));
            }
            var cs = ConnectionString.Parse(_options.Value.ConnectionString!);
            _client = new CosmosClient(cs.Endpoint, cs.SharedAccessKey,
                new CosmosClientOptions
                {
                    ConsistencyLevel = _options.Value?.Consistency
                });
        }

        /// <inheritdoc/>
        public async Task<IDatabase> OpenAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "default";
            }
            var response = await _client.CreateDatabaseIfNotExistsAsync(id,
                _options.Value?.ThroughputUnits).ConfigureAwait(false);
            return new DocumentDatabase(response.Database, _serializer, _logger);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.Dispose();
        }

        private readonly IOptions<CosmosDbOptions> _options;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _serializer;
        private readonly CosmosClient _client;
    }
}
