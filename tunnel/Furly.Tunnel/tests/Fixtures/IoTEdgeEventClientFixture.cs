// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.Tests
{
    using Furly.Azure;
    using Furly.Azure.IoT;
    using Furly.Azure.IoT.Edge;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading.Tasks;
    using HubResource = Furly.Azure.IoT.Edge.HubResource;

    public sealed class IoTEdgeEventClientFixture
    {
        public bool Skip { get; set; }

        /// <summary>
        /// Create test harness
        /// </summary>
        /// <returns></returns>
        internal IoTEdgeEventClientHarness GetHarness(string resource)
        {
            return new IoTEdgeEventClientHarness(resource, IoTHubServiceFixture.Up && !Skip);
        }
    }

    internal sealed class IoTEdgeEventClientHarness : IAsyncDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        internal IoTEdgeEventClientHarness(string resource, bool serviceUp)
        {
            if (!serviceUp)
            {
                _module = null;
                _hub = null;
                return;
            }
            try
            {
                // Read connections string from keyvault
                var config = new ConfigurationBuilder()
                    .AddFromDotEnvFile()
                    .Build();

                var builder = new ContainerBuilder();
                builder.AddConfiguration(config);
                builder.AddDefaultJsonSerializer();
                builder.AddIoTHubRpcClient();
                builder.AddIoTHubEventSubscriber();
                builder.AddIoTHubEventClient();

                builder.Configure<IoTHubDeviceOptions>(
                    options =>
                    {
                        HubResource.Parse(resource, out _, out var deviceId,
                            out var moduleId, out _);
                        options.DeviceId = deviceId;
                        options.ModuleId = moduleId;
                    });
                builder.AddLogging();
                _hub = builder.Build();

                // Create edge resource and get connection string for it
                var connectionString = CreateResourceAsync(resource).Result;
                resource = HubResource.Format(connectionString.HostName,
                    connectionString.DeviceId!, connectionString.ModuleId);

                // Create edge hosting container
                builder = new ContainerBuilder();
                builder.AddConfiguration(config);
                builder.AddDefaultJsonSerializer();
                builder.AddIoTEdgeServices();
                builder.Configure<IoTEdgeClientOptions>(
                    options => options.EdgeHubConnectionString = connectionString.ToString());
                builder.AddLogging();
                _module = builder.Build();

                _resource = resource;
            }
            catch
            {
                Try.Op(() => DeleteResourceAsync(resource).Wait());
                _hub?.Dispose();
                _hub = null;

                _module?.Dispose();
                _module = null;
            }
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient? GetModuleEventClient()
        {
            return Try.Op(() => _module?.Resolve<IEventClient>());
        }

        /// <summary>
        /// Get Event subscriber
        /// </summary>
        public IEventSubscriber? GetModuleEventSubscriber()
        {
            return Try.Op(() => _module?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Rpc server
        /// </summary>
        public IRpcServer? GetModuleRpcServer()
        {
            return Try.Op(() => _module?.Resolve<IRpcServer>());
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient? GetHubEventClient()
        {
            return Try.Op(() => _hub?.Resolve<IEventClient>());
        }

        /// <summary>
        /// Get Event subscriber
        /// </summary>
        public IEventSubscriber? GetHubEventSubscriber()
        {
            return Try.Op(() => _hub?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Method client
        /// </summary>
        public IRpcClient? GetHubRpcClient()
        {
            return Try.Op(() => _hub?.Resolve<IRpcClient>());
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_module != null)
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            if (_hub != null)
            {
                if (_resource != null)
                {
                    await DeleteResourceAsync(_resource).ConfigureAwait(false);
                }
                await _hub.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create device or module resource
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        internal async Task<ConnectionString> CreateResourceAsync(string resource)
        {
            var options = _hub?.Resolve<IOptions<IoTHubServiceOptions>>();
            var cs = options?.Value.ConnectionString;
            if (cs == null || !ConnectionString.TryParse(cs, out var hostCs))
            {
                throw new InvalidConfigurationException("Missing connection string");
            }
            using var registry = RegistryManager.CreateFromConnectionString(cs);
            await registry.OpenAsync().ConfigureAwait(false);
            if (!HubResource.Parse(resource, out _, out var deviceId, out var moduleId, out var error) ||
                moduleId == null)
            {
                throw new ArgumentException($"Invalid target {resource} provided ({error})");
            }
            // First try create device
            string? key = null;
            try
            {
                var device = await registry.AddDeviceAsync(
                    new Device(deviceId)).ConfigureAwait(false);
                key = device.Authentication.SymmetricKey.PrimaryKey;
            }
            catch (DeviceAlreadyExistsException)
            {
                // continue
            }

            // Try create module
            var module = await registry.AddModuleAsync(
                new Microsoft.Azure.Devices.Module(deviceId,
                moduleId)).ConfigureAwait(false);
            key = module.Authentication.SymmetricKey.PrimaryKey;

            return ConnectionString.CreateModuleConnectionString(hostCs.HostName!,
                deviceId, moduleId, key);
        }

        /// <summary>
        /// Delete device or module resource
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        internal async Task DeleteResourceAsync(string resource)
        {
            var options = _hub?.Resolve<IOptions<IoTHubServiceOptions>>();
            var cs = options?.Value.ConnectionString;
            if (cs == null)
            {
                return;
            }
            using var registry = RegistryManager.CreateFromConnectionString(cs);
            await registry.OpenAsync().ConfigureAwait(false);
            if (!HubResource.Parse(resource, out _, out var deviceId, out var moduleId, out var error))
            {
                throw new ArgumentException($"Invalid target {resource} provided ({error})");
            }
            await (string.IsNullOrEmpty(moduleId) ?
                registry.RemoveDeviceAsync(new Device(deviceId)
                {
                    ETag = "*"
                }) :
                registry.RemoveModuleAsync(new Microsoft.Azure.Devices.Module(
                    deviceId, moduleId)
                {
                    ETag = "*"
                })).ConfigureAwait(false);
        }

        private readonly IContainer? _module;
        private readonly IContainer? _hub;
        private readonly string? _resource;
    }
}
