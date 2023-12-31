// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    using Furly.Azure.IoT.Models;
    using Furly.Exceptions;
    using Furly.Extensions.Serializers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Twin services
    /// </summary>
    public interface IIoTHubTwinServices
    {
        /// <summary>
        /// Get the host name of the iot hub
        /// </summary>
        string HostName { get; }

        /// <summary>
        /// Create new twin or update existing one.  If there is
        /// a conflict and force is set, ensures the twin exists
        /// as specified in the end.
        /// </summary>
        /// <exception cref="ResourceConflictException"></exception>
        /// <param name="device">device twin to create</param>
        /// <param name="force">skip conflicting resource and update
        /// to the passed in twin state</param>
        /// <param name="ct"></param>
        /// <returns>new device</returns>
        ValueTask<DeviceTwinModel> CreateOrUpdateAsync(
            DeviceTwinModel device, bool force = false,
            CancellationToken ct = default);

        /// <summary>
        /// Update existing one.
        /// </summary>
        /// <exception cref="ResourceNotFoundException"></exception>
        /// <param name="device"></param>
        /// <param name="force">Do not use etag</param>
        /// <param name="ct"></param>
        /// <returns>new device</returns>
        ValueTask<DeviceTwinModel> PatchAsync(DeviceTwinModel device,
            bool force = false, CancellationToken ct = default);

        /// <summary>
        /// Returns twin
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<DeviceTwinModel> GetAsync(string deviceId,
            string? moduleId = null, CancellationToken ct = default);

        /// <summary>
        /// Returns registration info
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<DeviceTwinModel> GetRegistrationAsync(string deviceId,
            string? moduleId = null, CancellationToken ct = default);

        /// <summary>
        /// Query and return result and continuation
        /// </summary>
        /// <param name="query"></param>
        /// <param name="continuation"></param>
        /// <param name="pageSize"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<QueryResultModel> QueryAsync(string query,
            string? continuation = null, int? pageSize = null,
            CancellationToken ct = default);

        /// <summary>
        /// Query as device twin list
        /// </summary>
        /// <param name="query"></param>
        /// <param name="continuation"></param>
        /// <param name="pageSize"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<DeviceTwinListModel> QueryDeviceTwinsAsync(string query,
            string? continuation, int? pageSize = null,
            CancellationToken ct = default);

        /// <summary>
        /// Update device properties through twin
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="properties"></param>
        /// <param name="etag"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask UpdatePropertiesAsync(string deviceId, string moduleId,
            Dictionary<string, VariantValue> properties, string? etag = null,
            CancellationToken ct = default);

        /// <summary>
        /// Delete twin
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="etag"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask DeleteAsync(string deviceId, string? moduleId = null,
            string? etag = null, CancellationToken ct = default);
    }
}
