// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Zookeeper.Server
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
    /// Represents a Zookeeper node
    /// </summary>
    public class ZookeeperNode : DockerContainer
    {
        /// <summary>
        /// Create node
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="networkName"></param>
        /// <param name="port"></param>
        /// <param name="check"></param>
        internal ZookeeperNode(ILogger logger, string networkName, int? port = null,
            IHealthCheck? check = null) : base(logger, networkName, check)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _port = port ?? 2181;
        }

        /// <inheritdoc/>
        public async Task StartAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_containerId != null)
                {
                    return;
                }

                _logger.LogInformation("Starting Zookeeper node...");
                var param = GetContainerParameters(_port);
                var name = $"zookeeper_{_port}";
                (_containerId, _owner) = await CreateAndStartContainerAsync(
                    param, name, "bitnami/zookeeper:latest").ConfigureAwait(false);

                try
                {
                    // Check running
                    await WaitForContainerStartedAsync(_port).ConfigureAwait(false);
                    _logger.LogInformation("Zookeeper node running.");
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

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_containerId != null && _owner)
                {
                    await StopAndRemoveContainerAsync(_containerId).ConfigureAwait(false);
                    _logger.LogInformation("Stopped Zookeeper node...");
                }
            }
            finally
            {
                _containerId = null;
                _lock.Release();
            }
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
        /// Create create parameters
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private CreateContainerParameters GetContainerParameters(int port)
        {
            const int zooKeeperPort = 2181;
            return new CreateContainerParameters(
                new Config
                {
                    ExposedPorts = new Dictionary<string, EmptyStruct>()
                    {
                        [zooKeeperPort.ToString(CultureInfo.InvariantCulture)] = default
                    },
                    Env = [
                        "ZOO_ENABLE_AUTH=no",
                        "ALLOW_ANONYMOUS_LOGIN=yes"
                    ]
                })
            {
                HostConfig = new HostConfig
                {
                    NetworkMode = NetworkName,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [zooKeeperPort.ToString(CultureInfo.InvariantCulture)] = [
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
        private readonly int _port;
        private string? _containerId;
        private bool _owner;
    }
}
