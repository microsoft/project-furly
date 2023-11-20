// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Server
{
    using Furly.Extensions.Docker;
    using Furly.Extensions.Utils;
    using global::Docker.DotNet.Models;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a Kafka node
    /// </summary>
    public class KafkaNode : DockerContainer
    {
        /// <summary>
        /// Create node
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="zookeeper"></param>
        /// <param name="port"></param>
        /// <param name="networkName"></param>
        /// <param name="check"></param>
        internal KafkaNode(ILogger logger, string zookeeper, int port, string networkName,
            IHealthCheck? check = null) : base(logger, networkName, check)
        {
            _zookeeper = zookeeper ?? throw new ArgumentNullException(nameof(zookeeper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _port = port;
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

                _logger.LogInformation("Starting Kafka node at {Port}...", _port);
                var param = GetContainerParameters(_port);
                var name = $"kafka_{_port}";
                (_containerId, _owner) = await CreateAndStartContainerAsync(
                    param, name, "bitnami/kafka:latest").ConfigureAwait(false);
                try
                {
                    // Check running
                    await WaitForContainerStartedAsync(_port).ConfigureAwait(false);
                    _logger.LogInformation("Kafka node running at {Port}.", _port);
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
                    _logger.LogInformation("Stopped Kafka node at {Port}.", _port);
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
            const int kafkaPort = 9092;
            return new CreateContainerParameters(
                new Config
                {
                    ExposedPorts = new Dictionary<string, EmptyStruct>()
                    {
                        [kafkaPort.ToString(CultureInfo.InvariantCulture)] = default
                    },
                    Env = new List<string> {
                        $"KAFKA_CFG_ZOOKEEPER_CONNECT={_zookeeper}",
                        "ALLOW_PLAINTEXT_LISTENER=yes",
                        "KAFKA_CFG_LISTENERS=PLAINTEXT://0.0.0.0:"+ kafkaPort,
                        "KAFKA_CFG_ADVERTISED_LISTENERS=PLAINTEXT://" + HostIp.Value + ":" + port,
                    }
                })
            {
                HostConfig = new HostConfig
                {
                    NetworkMode = NetworkName,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [kafkaPort.ToString(CultureInfo.InvariantCulture)] = new List<PortBinding> {
                            new PortBinding {
                                HostPort = port.ToString(CultureInfo.InvariantCulture)
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Gets ip address
        /// </summary>
        private static Lazy<string> HostIp { get; } = new Lazy<string>(() =>
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? throw new InvalidOperationException();
                }
            }
            catch
            {
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .First(i => i.AddressFamily == AddressFamily.InterNetwork).ToString();
            }
        });

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger _logger;
        private readonly string _zookeeper;
        private readonly int _port;
        private string? _containerId;
        private bool _owner;
    }
}
