// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles events
    /// </summary>
    public interface IIoTHubTelemetryHandler
    {
        /// <summary>
        /// Handle telemetry received through IoT Hub via a device or module.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="topic"></param>
        /// <param name="data"></param>
        /// <param name="contentType"></param>
        /// <param name="contentEncoding"></param>
        /// <param name="properties"></param>
        /// <param name="ct"></param>
        ValueTask HandleAsync(string deviceId, string? moduleId, string topic,
            ReadOnlyMemory<byte> data, string contentType, string contentEncoding,
            IReadOnlyDictionary<string, string?> properties,
            CancellationToken ct = default);
    }
}
