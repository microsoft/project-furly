// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    using System;

    /// <summary>
    /// Transport types the client adapter should use
    /// </summary>
    [Flags]
    public enum TransportOption
    {
        /// <summary>
        /// No options
        /// </summary>
        None = 0,

        /// <summary>
        /// Amqp over tcp/ssl
        /// </summary>
        AmqpOverTcp = 0x1,

        /// <summary>
        /// Amqp over websocket
        /// </summary>
        AmqpOverWebsocket = 0x2,

        /// <summary>
        /// Amqp over tcp/ssl or websocket
        /// </summary>
        Amqp = AmqpOverTcp | AmqpOverWebsocket,

        /// <summary>
        /// Mqtt over tcp/ssl
        /// </summary>
        MqttOverTcp = 0x4,

        /// <summary>
        /// Tcp only
        /// </summary>
        Tcp = AmqpOverTcp | MqttOverTcp,

        /// <summary>
        /// Mqtt over websocket
        /// </summary>
        MqttOverWebsocket = 0x8,

        /// <summary>
        /// Websocket only
        /// </summary>
        Websocket = AmqpOverWebsocket | MqttOverWebsocket,

        /// <summary>
        /// Mqtt over tcp/ssl or websocket
        /// </summary>
        Mqtt = MqttOverTcp | MqttOverWebsocket,

        /// <summary>
        /// Use all possible transports
        /// </summary>
        Any = Amqp | Mqtt,
    }
}
