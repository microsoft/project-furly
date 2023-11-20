// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.Storage;
    using CouchDB.Driver;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides document db and graph functionality for storage interfaces.
    /// </summary>
    public class CouchDbClient : IDatabaseServer, IHealthCheck
    {
        /// <summary>
        /// Creates server
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public CouchDbClient(IOptions<CouchDbOptions> options, ILogger<CouchDbClient> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (_options.Value.HostName == null)
            {
                throw new ArgumentNullException(nameof(options), "Host name missing");
            }
        }

        /// <inheritdoc/>
        public Task<IDatabase> OpenAsync(string? id)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var client = new CouchClient("http://" + _options.Value.HostName + ":5984",
                builder =>
                {
                    builder = builder
                        .EnsureDatabaseExists()
                        .IgnoreCertificateValidation()
                        // ...
                        //.ConfigureFlurlClient(client => {
                        //  client.HttpClientFactory =
                        //})
                        ;
                    if (_options.Value.UserName is not null &&
                        _options.Value.Key is not null)
                    {
                        builder = builder
                            .UseBasicAuthentication(
                                _options.Value.UserName,
                                _options.Value.Key);
                    }
                });
#pragma warning restore CA2000 // Dispose objects before losing scope
            var db = new CouchDbDatabase(client, _logger);
            return Task.FromResult<IDatabase>(db);
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            var client = new CouchClient("http://" + _options.Value.HostName + ":5984",
                builder =>
                {
                    builder = builder
                        .EnsureDatabaseExists()
                        .IgnoreCertificateValidation();
                    if (_options.Value.UserName is not null &&
                        _options.Value.Key is not null)
                    {
                        builder = builder
                            .UseBasicAuthentication(
                                _options.Value.UserName,
                                _options.Value.Key);
                    }
                });
            try
            {
                // Try get last item
                var up = await client.IsUpAsync(cancellationToken).ConfigureAwait(false);
                return up ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Not up", ex);
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        private readonly IOptions<CouchDbOptions> _options;
        private readonly ILogger _logger;
    }
}
