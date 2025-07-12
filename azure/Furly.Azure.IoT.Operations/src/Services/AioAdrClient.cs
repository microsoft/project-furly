// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Connector.Files;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adr client
    /// </summary>
    public sealed class AioAdrClient : IAioAdrClient, IDisposable
    {
        /// <inheritdoc/>
        public IEnumerable<string> Devices => _client.GetDeviceNames();

        /// <inheritdoc/>
        public event EventHandler<DeviceChangedEventArgs> OnDeviceChanged
        {
            add
            {
                _client.DeviceChanged += value;
                _client.ObserveDevices(); // Start observing device changes
            }
            remove => _client.DeviceChanged -= value;
        }

        /// <inheritdoc/>
        public event EventHandler<AssetChangedEventArgs> OnAssetChanged
        {
            add => _client.AssetChanged += value;
            remove => _client.AssetChanged -= value;
        }

        /// <summary>
        /// Create aio adr client
        /// </summary>
        /// <param name="sdk"></param>
        /// <param name="client"></param>
        /// <param name="logger"></param>
        public AioAdrClient(IAioSdk sdk,
            IMqttPubSubClient client, ILogger<AioAdrClient> logger)
        {
            _logger = logger;
            _client = sdk.CreateAdrClientWrapper(client);

            // Any devices already available will trigger the notifications
            _logger.StartMonitoring(client.ClientId);
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
            catch (Exception ex)
            {
                _logger.StopMonitoringFailed(ex);
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

        private readonly ILogger _logger;
        private readonly IAdrClientWrapper _client;
    }

    /// <summary>
    /// Source-generated logging for AioAdrClient
    /// </summary>
    internal static partial class AioAdrClientLogging
    {
        private const int EventClass = 10;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Start ADR service client {ClientId}")]
        public static partial void StartMonitoring(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Stop ADR service client.")]
        public static partial void StopMonitoring(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Debug,
            Message = "Error stopping and disposing ADR service client.")]
        public static partial void StopMonitoringFailed(this ILogger logger, Exception ex);
    }
}
