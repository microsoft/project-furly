// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Exceptions
{
    using Furly.Extensions.Mqtt.Runtime;
    using Furly.Exceptions;
    using System;

    /// <summary>
    /// Thrown if the message is removed from the queue because
    /// the message queue size was reached. Depending on the
    /// <see cref="MqttOptions.OverflowStrategy"/>,
    /// this either signals that this message was the first message
    /// in the queue when the max queue size was reached or
    /// that this message tried to be enqueued when the queue was
    /// already at the max queue size.
    /// </summary>
    public class QueueOverflowException : ExternalDependencyException
    {
        /// <summary>
        /// Overflow strategy if available
        /// </summary>
        public OverflowStrategy? MessagePurgeStrategy { get; }

        /// <inheritdoc/>
        public QueueOverflowException(
            OverflowStrategy? messagePurgeStrategy)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }

        /// <inheritdoc/>
        public QueueOverflowException(
            OverflowStrategy? messagePurgeStrategy,
            string message) : base(message)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }

        /// <inheritdoc/>
        public QueueOverflowException(
            OverflowStrategy? messagePurgeStrategy,
            string message, Exception innerException)
            : base(message, innerException)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }

        /// <inheritdoc/>
        public QueueOverflowException()
        {
        }

        /// <inheritdoc/>
        public QueueOverflowException(string message)
            : base(message)
        {
        }

        /// <inheritdoc/>
        public QueueOverflowException(string message,
            Exception innerException) : base(message, innerException)
        {
        }
    }
}
