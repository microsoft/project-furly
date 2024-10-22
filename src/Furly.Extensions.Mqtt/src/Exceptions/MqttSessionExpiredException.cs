// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Exceptions
{
    using Furly.Exceptions;
    using System;

    /// <summary>
    /// This exception is thrown when an MQTT session could
    /// not be recovered by the client before it expired on
    /// the broker.
    /// </summary>
    /// <remarks>
    /// The session expiry interval can be set when first
    /// establishing a connection. If the client loses
    /// connection to the broker and then that interval passes
    /// without the client successfully reconnecting, then
    /// the broker will discard the session. Upon a successful
    /// reconnection after this happens, this exception
    /// To avoid this exception, longer values of the session
    /// expiry interval are recommended.
    /// </remarks>
    public class MqttSessionExpiredException : ExternalDependencyException
    {
        /// <inheritdoc/>
        public MqttSessionExpiredException()
        {
        }

        /// <inheritdoc/>
        public MqttSessionExpiredException(string message)
            : base(message)
        {
        }

        /// <inheritdoc/>
        public MqttSessionExpiredException(string message,
            Exception innerException) : base(message, innerException)
        {
        }
    }
}
