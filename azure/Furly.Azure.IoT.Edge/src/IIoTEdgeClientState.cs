// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    /// <summary>
    /// Callback handler for client state
    /// </summary>
    public interface IIoTEdgeClientState
    {
        /// <summary>
        /// Opened
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        void OnOpened(string deviceId, string? moduleId);

        /// <summary>
        /// Device or module connected with the specified reason
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="reason"></param>
        void OnConnected(int counter, string deviceId,
            string? moduleId, string reason);

        /// <summary>
        /// Device or module connected with the specified reason
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="reason"></param>
        void OnDisconnected(int counter, string deviceId,
            string? moduleId, string reason);

        /// <summary>
        /// On closed
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="reason"></param>
        void OnClosed(int counter, string deviceId,
            string? moduleId, string reason);
    }
}
