// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
    using global::Azure.Iot.Operations.Connector.Assets;

    /// <summary>
    /// Akri connector client
    /// </summary>
    public interface IAioAdrClient : IAsyncDisposable
    {
        /// <summary>
        /// Register for asset notification for a found device. This should be
        /// called for all device endpoints we are interested in.
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask StartMonitoringAssetsAsync(string deviceName, string inboundEndpointName,
            CancellationToken ct = default);

        /// <summary>
        /// Unobserve asset changes for a endpoint
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask StopMonitoringAssetsAsync(string deviceName, string inboundEndpointName,
            CancellationToken ct = default);

        /// <summary>
        /// Get credentials for endpoint
        /// </summary>
        /// <param name="inboundEndpoint"></param>
        /// <returns></returns>
        EndpointCredentials GetEndpointCredentials(InboundEndpointSchemaMapValue inboundEndpoint);

        /// <summary>
        /// Update status of an asset
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="request"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName,
            UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null,
            CancellationToken ct = default);

        /// <summary>
        /// Update status of a device
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="status"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName,
            DeviceStatus status, TimeSpan? commandTimeout = null,
            CancellationToken ct = default);

        /// <summary>
        /// Report a new discovered asset
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="assetName"></param>
        /// <param name="asset"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<DiscoveredAssetResponseSchema> ReportDiscoveredAssetAsync(string deviceName,
            string inboundEndpointName, string assetName, DiscoveredAsset asset,
            TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Report a new discovered device
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="device"></param>
        /// <param name="inboundEndpointType"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<DiscoveredDeviceResponseSchema> ReportDiscoveredDeviceAsync(string deviceName,
            DiscoveredDevice device, string inboundEndpointType, TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default);
    }
}
