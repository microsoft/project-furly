// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    /// <summary>
    /// Configuration for device
    /// </summary>
    public class IoTHubDeviceOptions
    {
        /// <summary>
        /// Device id
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Module id
        /// </summary>
        public string? ModuleId { get; set; }
    }
}
