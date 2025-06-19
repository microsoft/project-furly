// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Server
{
    using Furly.Extensions.Docker;
    using Furly.Extensions.Utils;
    using global::Docker.DotNet.Models;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a CouchDB node
    /// </summary>
    public class CouchDbServer : DockerContainer
    {
        /// <summary>
        /// Create node
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="port"></param>
        /// <param name="check"></param>
        public CouchDbServer(ILogger<CouchDbServer> logger, string? user = null,
            string? key = null, int? port = null, IHealthCheck? check = null) :
            base(logger, null, check)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _user = user;
            _key = key;
            _port = port ?? 5984;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Try.Op(() => StopAsync().GetAwaiter().GetResult());
                _lock.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Start the server
        /// </summary>
        public async Task StartAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_containerId != null)
                {
                    return;
                }

                _logger.ServerStarting(_port);
                var param = GetContainerParameters(_port);
                var name = $"couchdb_{_port}";
                (_containerId, _owner) = await CreateAndStartContainerAsync(
                    param, name, "bitnami/couchdb:latest").ConfigureAwait(false);

                try
                {
                    // Check running
                    await WaitForContainerStartedAsync(_port).ConfigureAwait(false);
                    _logger.ServerRunning(_port);
                }
                catch
                {
                    // Stop and retry
                    await StopAndRemoveContainerAsync(_containerId).ConfigureAwait(false);
                    _containerId = null;
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        public async Task StopAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_containerId != null && _owner)
                {
                    await StopAndRemoveContainerAsync(_containerId).ConfigureAwait(false);
                    _logger.ServerStopped(_port);
                }
            }
            finally
            {
                _containerId = null;
                _lock.Release();
            }
        }

        /// <summary>
        /// Create create parameters
        /// </summary>
        /// <param name="port"></param>
        private CreateContainerParameters GetContainerParameters(int port)
        {
            const int couchPort = 5984;
            return new CreateContainerParameters(
                new Config
                {
                    ExposedPorts = new Dictionary<string, EmptyStruct>()
                    {
                        [couchPort.ToString(CultureInfo.InvariantCulture)] = default
                    },
                    Env = [
                        "COUCHDB_CREATE_DATABASES=yes",
                        "COUCHDB_USER=" + _user ?? "admin",
                        "COUCHDB_PASSWORD=" + _key ?? "couchdb",
                    ]
                })
            {
                HostConfig = new HostConfig
                {
                    NetworkMode = NetworkName,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [couchPort.ToString(CultureInfo.InvariantCulture)] = [
                            new() {
                                HostPort = port.ToString(CultureInfo.InvariantCulture)
                            }
                        ]
                    }
                }
            };
        }

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger _logger;
        private readonly string? _user;
        private readonly string? _key;
        private readonly int _port;
        private string? _containerId;
        private bool _owner;
    }

    /// <summary>
    /// Source-generated logging for CouchDbServer tests
    /// </summary>
    internal static partial class CouchDbServerTestsLogging
    {
        private const int EventClass = 1;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Starting CouchDB server at {Port}...")]
        public static partial void ServerStarting(this ILogger logger, int port);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "CouchDB server running at {Port}.")]
        public static partial void ServerRunning(this ILogger logger, int port);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Stopped CouchDB server at {Port}.")]
        public static partial void ServerStopped(this ILogger logger, int port);
    }
}
