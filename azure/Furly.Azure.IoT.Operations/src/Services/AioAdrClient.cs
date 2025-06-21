// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Connector.Files;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
    using k8s.KubeConfigModels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adr client
    /// </summary>
    public sealed class AioAdrClient : IAioAdrClient, IDisposable
    {
        /// <inheritdoc/>
        public IEnumerable<string> Devices => _client.GetDeviceNames();

        /// <summary>
        /// Create aio sr client
        /// </summary>
        /// <param name="notifications"></param>
        /// <param name="sdk"></param>
        /// <param name="client"></param>
        /// <param name="logger"></param>
        public AioAdrClient(IAdrNotification notifications, IAioSdk sdk,
            IMqttPubSubClient client, ILogger<AioAdrClient> logger)
        {
            _logger = logger;
            _notifications = notifications;
            _client = sdk.CreateAdrClientWrapper(client);
            _client.AssetChanged += OnAssetChanged;
            _client.DeviceChanged += OnDeviceChanged;

            // Any devices already available will trigger the notifications
            _logger.StartMonitoring(client.ClientId);
            _client.ObserveDevices();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                _logger.StopMonitoring();
                await _client.UnobserveAllAsync(default).ConfigureAwait(false);
            }
            finally
            {
                await _client.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public ValueTask StartMonitoringAssetsAsync(string deviceName, string inboundEndpointName,
            CancellationToken ct)
        {
            // Any pre-existing assets will trigger the monitor's callback
            // which triggers the ADR client to observe updates
            _client.ObserveAssets(deviceName, inboundEndpointName);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public async ValueTask StopMonitoringAssetsAsync(string deviceName, string inboundEndpointName,
            CancellationToken ct)
        {
            await _client.UnobserveAssetsAsync(deviceName, inboundEndpointName,
                ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName,
            InboundEndpointSchemaMapValue settings)
        {
            return _client.GetEndpointCredentials(deviceName, inboundEndpointName, settings);
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceStatus> UpdateDeviceStatusAsync(string deviceName,
            string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout,
            CancellationToken ct)
        {
            return await _client.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, status,
                commandTimeout, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<AssetStatus> UpdateAssetStatusAsync(string deviceName,
            string inboundEndpointName, string assetName, AssetStatus status,
            TimeSpan? commandTimeout, CancellationToken ct)
        {
            return await _client.UpdateAssetStatusAsync(deviceName, inboundEndpointName,
                new UpdateAssetStatusRequest
                {
                    AssetName = assetName,
                    AssetStatus = status
                }, commandTimeout, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<DiscoveredAssetResponseSchema> ReportDiscoveredAssetAsync(
            string deviceName, string inboundEndpointName, string assetName, DiscoveredAsset asset,
            TimeSpan? commandTimeout, CancellationToken cancellationToken)
        {
            var response = await _client.CreateOrUpdateDiscoveredAssetAsync(deviceName,
                inboundEndpointName, new CreateOrUpdateDiscoveredAssetRequest
                {
                    DiscoveredAsset = asset,
                    DiscoveredAssetName = assetName
                },
                commandTimeout, cancellationToken).ConfigureAwait(false);
            return response.DiscoveredAssetResponse;
        }

        /// <inheritdoc/>
        public async ValueTask<DiscoveredDeviceResponseSchema> ReportDiscoveredDeviceAsync(
            string deviceName, DiscoveredDevice device, string inboundEndpointType,
            TimeSpan? commandTimeout, CancellationToken cancellationToken)
        {
            var response = await _client.CreateOrUpdateDiscoveredDeviceAsync(
                new CreateOrUpdateDiscoveredDeviceRequestSchema
                {
                    DiscoveredDevice = device,
                    DiscoveredDeviceName = deviceName
                }, inboundEndpointType, commandTimeout, cancellationToken).ConfigureAwait(false);
            return response.DiscoveredDeviceResponse;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            return _client.GetAssetNames(deviceName, inboundEndpointName);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            return _client.GetInboundEndpointNames(deviceName);
        }

        internal void OnDeviceChanged(object? sender,
            global::Azure.Iot.Operations.Connector.DeviceChangedEventArgs e)
        {
            _logger.DeviceChanged(e.DeviceName, e.InboundEndpointName, e.ChangeType);
            switch (e.ChangeType)
            {
                case ChangeType.Deleted:
                    _notifications.OnDeviceDeleted(e.DeviceName, e.InboundEndpointName, e.Device);
                    break;
                case ChangeType.Created:
                    Debug.Assert(e.Device != null);
                    _notifications.OnDeviceCreated(e.DeviceName, e.InboundEndpointName, e.Device);
                    break;
                case ChangeType.Updated:
                    Debug.Assert(e.Device != null);
                    _notifications.OnDeviceUpdated(e.DeviceName, e.InboundEndpointName, e.Device);
                    break;
            }
        }

        internal void OnAssetChanged(object? sender,
            global::Azure.Iot.Operations.Connector.AssetChangedEventArgs e)
        {
            _logger.AssetChanged(e.AssetName, e.DeviceName, e.InboundEndpointName, e.ChangeType);
            switch (e.ChangeType)
            {
                case ChangeType.Deleted:
                    _notifications.OnAssetDeleted(e.DeviceName, e.InboundEndpointName, e.AssetName, e.Asset);
                    break;
                case ChangeType.Created:
                    Debug.Assert(e.Asset != null);
                    _notifications.OnAssetCreated(e.DeviceName, e.InboundEndpointName, e.AssetName, e.Asset);
                    break;
                case ChangeType.Updated:
                    Debug.Assert(e.Asset != null);
                    _notifications.OnAssetUpdated(e.DeviceName, e.InboundEndpointName, e.AssetName, e.Asset);
                    break;
            }
        }

        private readonly ILogger _logger;
        private readonly IAdrClientWrapper _client;
        private readonly IAdrNotification _notifications;
    }

    /// <summary>
    /// Source-generated logging for AioAdrClient
    /// </summary>
    internal static partial class AioAdrClientLogging
    {
        private const int EventClass = 10;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Start monitoring ADR devices using client {ClientId}")]
        public static partial void StartMonitoring(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Stop monitoring ADR devices.")]
        public static partial void StopMonitoring(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "Device with name {Name} and endpoint {EndpointName} was {Action}")]
        public static partial void DeviceChanged(this ILogger logger, string name, string endpointName, ChangeType action);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Information,
            Message = "Asset with name {Name} for device {Device} and endpoint {EndpointName} was {Action}")]
        public static partial void AssetChanged(this ILogger logger, string name, string device, string endpointName, ChangeType action);
    }
}
