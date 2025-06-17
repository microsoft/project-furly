// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

    /// <summary>
    /// Adr notifications
    /// </summary>
    public interface IAdrNotification
    {
        /// <summary>
        /// Device created
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="device"></param>
        void OnDeviceCreated(string deviceName, string inboundEndpointName,
            Device device);

        /// <summary>
        /// Device updated
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="device"></param>
        void OnDeviceUpdated(string deviceName, string inboundEndpointName,
            Device device);

        /// <summary>
        /// Device removed
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="device"></param>
        void OnDeviceDeleted(string deviceName, string inboundEndpointName,
            Device? device);

        /// <summary>
        /// Asset created
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="assetName"></param>
        /// <param name="asset"></param>
        void OnAssetCreated(string deviceName, string inboundEndpointName,
            string assetName, Asset asset);

        /// <summary>
        /// Asset updated
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="assetName"></param>
        /// <param name="asset"></param>
        void OnAssetUpdated(string deviceName, string inboundEndpointName,
            string assetName, Asset asset);

        /// <summary>
        /// Asset deleted
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="inboundEndpointName"></param>
        /// <param name="assetName"></param>
        /// <param name="asset"></param>
        void OnAssetDeleted(string deviceName, string inboundEndpointName,
            string assetName, Asset? asset);
    }
}
