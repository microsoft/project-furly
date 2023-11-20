// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Docker
{
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a rabbit mq server instance
    /// </summary>
    public abstract class DockerContainer : IDisposable
    {
        /// <summary>
        /// Network name
        /// </summary>
        public string? NetworkName { get; set; }

        /// <summary>
        /// Container name
        /// </summary>
        public string? ContainerName { get; set; }

        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="networkName"></param>
        /// <param name="check"></param>
        protected DockerContainer(ILogger logger, string? networkName = null,
            IHealthCheck? check = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _check = check;
            NetworkName = networkName;
        }

        /// <summary>
        /// Dispose object
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _lock.Dispose();
                }
                _disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start container
        /// </summary>
        /// <param name="containerParameters"></param>
        /// <param name="containerName"></param>
        /// <param name="imageName"></param>
        /// <param name="ct"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected async Task<(string, bool)> CreateAndStartContainerAsync(
            CreateContainerParameters containerParameters, string containerName,
            string imageName, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(containerParameters);

            using (var dockerClient = CreateDockerClient())
            {
                ContainerName = containerName;
                containerParameters.Image = imageName;
                await CreateNetworkIfNotExistsAsync(dockerClient).ConfigureAwait(false);

                var containers = await dockerClient.Containers.ListContainersAsync(
                    new ContainersListParameters { All = true }, ct).ConfigureAwait(false);
                var existingContainer = containers
                    .SingleOrDefault(c => c.Names.Contains("/" + containerName));
                if (existingContainer != null)
                {
                    // Remove existing container
                    await StopAndRemoveContainerAsync(
                        dockerClient, existingContainer.ID, ct).ConfigureAwait(false);
                }

                var images = await dockerClient.Images.ListImagesAsync(
                    new ImagesListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["reference"] = new Dictionary<string, bool>
                            {
                                [imageName] = true
                            }
                        }
                    }, ct).ConfigureAwait(false);
                if (images.Count == 0)
                {
                    var tag = imageName.Split(':').Last();
                    var imagesCreateParameters = new ImagesCreateParameters
                    {
                        FromImage = imageName,
                        Tag = tag,
                    };
                    await dockerClient.Images.CreateImageAsync(
                        imagesCreateParameters, new AuthConfig(),
                            new Progress<JSONMessage>(m =>
                            {
                                if (m.Error != null)
                                {
                                    _logger.LogError("{Message}", m.Error.Message);
                                }
                            }), ct).ConfigureAwait(false);
                }
                containerParameters.Name = containerName;
                if (!string.IsNullOrEmpty(NetworkName))
                {
                    containerParameters.HostConfig ??= new HostConfig();
                    containerParameters.HostConfig.NetworkMode = NetworkName;
                }
                var container = await dockerClient.Containers.CreateContainerAsync(
                    containerParameters, ct).ConfigureAwait(false);
                var containerId = container.ID;
                await dockerClient.Containers.StartContainerAsync(containerId,
                    new ContainerStartParameters(), ct).ConfigureAwait(false);
                return (containerId, true);
            }
        }

        /// <summary>
        /// Wait to start
        /// </summary>
        /// <param name="port"></param>
        /// <exception cref="TimeoutException"></exception>
        protected async Task WaitForContainerStartedAsync(int port)
        {
            var attempts = 0;
            var sw = Stopwatch.StartNew();
            var ep = new IPEndPoint(IPAddress.Loopback, port);
            do
            {
                if (await CheckAvailabilityAsync(ep).ConfigureAwait(false))
                {
                    return;
                }
                await Task.Delay(1000).ConfigureAwait(false);
            } while (attempts++ <= 60);
            sw.Stop();
            throw new TimeoutException($"Container failed to start after {sw.Elapsed}.)");
        }

        /// <summary>
        /// Stop container
        /// </summary>
        /// <param name="containerId"></param>
        /// <param name="ct"></param>
        protected async Task StopAndRemoveContainerAsync(string containerId,
            CancellationToken ct = default)
        {
            using (var dockerClient = CreateDockerClient())
            {
                await StopAndRemoveContainerAsync(dockerClient, containerId,
                    ct).ConfigureAwait(false);
                ContainerName = null;
            }
        }

        /// <summary>
        /// Helper to stop and remove container
        /// </summary>
        /// <param name="dockerClient"></param>
        /// <param name="containerId"></param>
        /// <param name="ct"></param>
        private static async Task StopAndRemoveContainerAsync(DockerClient dockerClient,
            string containerId, CancellationToken ct)
        {
            var container = await dockerClient.Containers.InspectContainerAsync(
                containerId, ct).ConfigureAwait(false);
            if (container.State.Running)
            {
                await dockerClient.Containers.StopContainerAsync(containerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 1 }, ct).ConfigureAwait(false);
            }
            await dockerClient.Containers.RemoveContainerAsync(containerId,
                new ContainerRemoveParameters { Force = true }, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for container to have started
        /// </summary>
        /// <param name="ep"></param>
        private async Task<bool> CheckAvailabilityAsync(IPEndPoint ep)
        {
            try
            {
                if (_check != null)
                {
                    // Run health check against container
                    var result = await _check.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(false);
                    return result.Status == HealthStatus.Healthy;
                }

                using (var sender = new Socket(IPAddress.Loopback.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp))
                {
                    await sender.ConnectAsync(ep).ConfigureAwait(false);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper to create network if needed
        /// </summary>
        /// <param name="dockerClient"></param>
        private async Task CreateNetworkIfNotExistsAsync(DockerClient dockerClient)
        {
            if (string.IsNullOrEmpty(NetworkName))
            {
                return;
            }
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var networks = await dockerClient.Networks.ListNetworksAsync(
                    new NetworksListParameters()).ConfigureAwait(false);
                var existing = networks.Where(n => n.Name == NetworkName).ToList();
                if (existing.Count > 1)
                {
                    // Remove ambigous networks
                    foreach (var network in existing)
                    {
                        await dockerClient.Networks.DeleteNetworkAsync(network.ID).ConfigureAwait(false);
                    }
                    existing.Clear();
                }
                if (existing.Count == 0)
                {
                    await dockerClient.Networks.CreateNetworkAsync(
                        new NetworksCreateParameters { Name = NetworkName }).ConfigureAwait(false);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Create docker client
        /// </summary>
        private static DockerClient CreateDockerClient()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            return new DockerClientConfiguration(new Uri(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "npipe://./pipe/docker_engine"
                    : "unix:///var/run/docker.sock"
            )).CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        private readonly ILogger _logger;
        private readonly IHealthCheck? _check;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _disposedValue;
    }
}
