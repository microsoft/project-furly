// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IoT Hub device client abstraction
    /// </summary>
    public interface IIoTEdgeDeviceClient : IDisposable
    {
        /// <summary>
        /// Sends an event to device hub
        /// </summary>
        /// <param name="message"></param>
        /// <param name="output"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task SendEventAsync(Message message, string? output = null,
            CancellationToken ct = default);

        /// <summary>
        /// Sends a batch of events to device hub
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="output"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task SendEventBatchAsync(IEnumerable<Message> messages,
            string? output = null, CancellationToken ct = default);

        /// <summary>
        /// Registers a new delegate that is called for a method that
        /// doesn't have a delegate registered for its name.
        /// If a default delegate is already registered it will replace
        /// with the new delegate.
        /// </summary>
        /// <param name="methodHandler">The delegate to be used when
        /// a method is called by the cloud service and there is no
        /// delegate registered for that method name.</param>
        /// <param name="userContext">Generic parameter to be interpreted
        /// by the client code.</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task SetMethodHandlerAsync(MethodCallback? methodHandler,
            object? userContext, CancellationToken ct = default);

        /// <summary>
        /// Registers a new delegate that is called for each received
        /// message.
        /// </summary>
        /// <param name="messageHandler">The delegate to be used when
        /// a message is received.</param>
        /// <param name="userContext">Generic parameter to be interpreted
        /// by the client code.</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task SetMessageHandlerAsync(MessageHandler? messageHandler,
            object? userContext, CancellationToken ct = default);

        /// <summary>
        /// Interactively invokes a method on module
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="methodRequest"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<MethodResponse> InvokeMethodAsync(string deviceId, string moduleId,
            MethodRequest methodRequest, CancellationToken ct = default);

        /// <summary>
        /// Interactively invokes a method on a device.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="methodRequest"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<MethodResponse> InvokeMethodAsync(string deviceId,
            MethodRequest methodRequest, CancellationToken ct = default);

        /// <summary>
        /// Retrieve a device twin object for the current device.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns>The device twin object for the current device</returns>
        Task<Twin> GetTwinAsync(CancellationToken ct = default);

        /// <summary>
        /// Set a callback that will be called whenever the client
        /// receives a state update (desired or reported) from the service.
        /// </summary>
        /// <param name="callback">Callback to call after the state
        /// update has been received and applied</param>
        /// <param name="userContext"></param>
        /// <param name="ct"></param>
        Task SetDesiredPropertyUpdateCallbackAsync(
            DesiredPropertyUpdateCallback callback, object? userContext,
            CancellationToken ct = default);

        /// <summary>
        /// Push reported property changes up to the service.
        /// </summary>
        /// <param name="reportedProperties">Reported properties to push</param>
        /// <param name="ct"></param>
        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties,
            CancellationToken ct = default);

        /// <summary>
        /// Close the DeviceClient instance
        /// </summary>
        /// <returns></returns>
        Task CloseAsync();
    }
}
