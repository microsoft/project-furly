// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock
{
    using Furly.Azure.IoT.Models;
    using System.Collections.Generic;

    /// <summary>
    /// Hub interface
    /// </summary>
    public interface IIoTHub
    {
        /// <summary>
        /// The host name the client is talking to
        /// </summary>
        string HostName { get; }

        /// <summary>
        /// List of devices for devices queries
        /// </summary>
        IEnumerable<DeviceTwinModel> Devices { get; }

        /// <summary>
        /// List of modules for module queries
        /// </summary>
        IEnumerable<DeviceTwinModel> Modules { get; }

        /// <summary>
        /// Connect device/module to hub
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <returns></returns>
        IIoTHubConnection? Connect(string deviceId, string moduleId);
    }
}
