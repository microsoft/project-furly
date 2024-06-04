// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure;
    using Furly.Exceptions;
    using Docker.DotNet.Models;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an IoT Edge device containerized
    /// </summary>
    public sealed class IoTEdgeDevice : DockerContainer, IAwaitable<IoTEdgeDevice>, IAsyncDisposable
    {
        /// <summary>
        /// Create device
        /// </summary>
        /// <param name="options"></param>
        /// <param name="deviceId"></param>
        /// <param name="logger"></param>
        /// <param name="ports"></param>
        /// <param name="check"></param>
        public IoTEdgeDevice(IOptions<IoTHubServiceOptions> options, string deviceId,
            ILogger<IoTEdgeDevice> logger, int[] ports = null, IHealthCheck check = null) :
            this(GetConnectionStringAsync(options.Value, deviceId).Result,
                logger, ports, check)
        {
            _options = options;
        }

        /// <summary>
        /// Create device
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="logger"></param>
        /// <param name="ports"></param>
        /// <param name="check"></param>
        /// <exception cref="ArgumentNullException"></exception>
        internal IoTEdgeDevice(ConnectionString cs, ILogger<IoTEdgeDevice> logger,
            int[] ports = null, IHealthCheck check = null) : base(logger, null, check)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cs = cs ?? throw new ArgumentNullException(nameof(cs));
            if (ports == null || ports.Length == 0)
            {
                ports = [15580, 15581, 1883, 8883, 5276, 443]; // TODO
            }
            _ports = ports;
            _start = StartAsync();
        }

        /// <inheritdoc/>
        public IAwaiter<IoTEdgeDevice> GetAwaiter()
        {
            return _start.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public async Task StartAsync()
        {
            _logger.LogInformation("Starting IoTEdge device...");
            var param = GetContainerParameters(_ports);
            var name = $"iotedge_{string.Join("_", _ports)}";
            (_containerId, _owner) = await CreateAndStartContainerAsync(
                param, name, "furlysoft/iotedge:latest").ConfigureAwait(false);

            try
            {
                // Check running
                await WaitForContainerStartedAsync(
                    _ports[0]).ConfigureAwait(false);
                _logger.LogInformation("IoTEdge device running.");
            }
            catch
            {
                // Stop and retry
                await StopAndRemoveContainerAsync(
                    _containerId).ConfigureAwait(false);
                _containerId = null;
                throw;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_containerId != null && _owner)
            {
                await StopAndRemoveContainerAsync(
                    _containerId).ConfigureAwait(false);
                _logger.LogInformation("Stopped IoTEdge device.");
            }
            await DeleteGatewayAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Create create parameters
        /// </summary>
        /// <param name="hostPorts"></param>
        /// <returns></returns>
        private CreateContainerParameters GetContainerParameters(int[] hostPorts)
        {
            var containerPorts = new[] { 15580, 15581, 1883, 8883, 5276, 443 };
            return new CreateContainerParameters(
                new Config
                {
                    Hostname = _cs.DeviceId,
                    ExposedPorts = containerPorts
                        .ToDictionary<int, string, EmptyStruct>(
                            p => p.ToString(CultureInfo.InvariantCulture), _ => default),
                    Env = new List<string> {
                        "connectionString=" + _cs
                    }
                })
            {
                Name = _cs.DeviceId,
                HostConfig = new HostConfig
                {
                    Privileged = true,
                    PortBindings = containerPorts.ToDictionary(k => k.ToString(CultureInfo.InvariantCulture),
                    v => (IList<PortBinding>)new List<PortBinding> {
                        new () {
                            HostPort = hostPorts[Array.IndexOf(containerPorts, v) %
                                hostPorts.Length].ToString(CultureInfo.InvariantCulture)
                        }
                    })
                }
            };
        }

        /// <summary>
        /// Delete device
        /// </summary>
        private async Task DeleteGatewayAsync()
        {
            var cs = _options?.Value.ConnectionString;
            if (cs == null)
            {
                return;
            }
            using var registry = RegistryManager.CreateFromConnectionString(cs);
            await registry.OpenAsync().ConfigureAwait(false);
            await registry.RemoveDeviceAsync(new Device(_cs.DeviceId)
            {
                ETag = "*"
            }).ConfigureAwait(false);
            _logger.LogInformation("IoTEdge device removed from hub.");
        }

        /// <summary>
        /// Create gateway if it does not exist and return connection string.
        /// </summary>
        private static async Task<ConnectionString> GetConnectionStringAsync(
            IoTHubServiceOptions options, string deviceId)
        {
            var cs = options.ConnectionString;
            if (cs == null || !ConnectionString.TryParse(cs, out var hostCs))
            {
                throw new InvalidConfigurationException("Missing connection string");
            }
            using var registry = RegistryManager.CreateFromConnectionString(cs);
            await registry.OpenAsync().ConfigureAwait(false);
            // First try create device
            Device device;
            try
            {
                device = await registry.AddDeviceAsync(
                    new Device(deviceId)).ConfigureAwait(false);
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registry.GetDeviceAsync(deviceId).ConfigureAwait(false);
            }
            await registry.UpdateTwinAsync(deviceId, new Twin
            {
                Capabilities = new DeviceCapabilities { IotEdge = true }
            }, device.ETag).ConfigureAwait(false);
            return ConnectionString.CreateDeviceConnectionString(hostCs.HostName,
                deviceId, device.Authentication.SymmetricKey.PrimaryKey);
        }

        private readonly ILogger _logger;
        private readonly ConnectionString _cs;
        private readonly int[] _ports;
        private readonly Task _start;
        private readonly IOptions<IoTHubServiceOptions> _options;
        private string _containerId;
        private bool _owner;
    }
}
