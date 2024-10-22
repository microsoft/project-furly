// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Exceptions
{
    using Furly.Exceptions;
    using MQTTnet;
    using System;

    /// <summary>
    /// Connection exception
    /// </summary>
    public sealed class ConnectingFailedException : ExternalDependencyException
    {
        /// <summary>
        /// Result
        /// </summary>
        public MqttClientConnectResult Result { get; }

        /// <summary>
        /// Result code
        /// </summary>
        public MqttClientConnectResultCode ResultCode
            => Result?.ResultCode ?? MqttClientConnectResultCode.UnspecifiedError;

        /// <inheritdoc/>
        public ConnectingFailedException(string message,
            MqttClientConnectResult connectResult) : base(message)
        {
            Result = connectResult;
        }

        /// <inheritdoc/>
        public ConnectingFailedException()
        {
            Result = new MqttClientConnectResult();
        }

        /// <inheritdoc/>
        public ConnectingFailedException(string message)
            : base(message)
        {
            Result = new MqttClientConnectResult();
        }

        /// <inheritdoc/>
        public ConnectingFailedException(string message,
            Exception innerException) : base(message, innerException)
        {
            Result = new MqttClientConnectResult();
        }
    }
}
