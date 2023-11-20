// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Storage;

    /// <summary>
    /// Represents the client end of a connection to iot hub.
    /// </summary>
    public interface IIoTHubConnection
    {
        /// <summary>
        /// Get rpc server interface on the connection
        /// </summary>
        IRpcServer RpcServer { get; }

        /// <summary>
        /// Get event client interface on the connection
        /// </summary>
        IEventClient EventClient { get; }

        /// <summary>
        /// Get access to the twin
        /// </summary>
        IKeyValueStore Twin { get; }

        /// <summary>
        /// Close connection.
        /// </summary>
        void Close();
    }
}
