﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Event to send
    /// </summary>
    public interface IEvent : IDisposable
    {
        /// <summary>
        /// Output path to use
        /// </summary>
        IEvent SetTopic(string? value);

        /// <summary>
        /// Processing timestamp
        /// </summary>
        IEvent SetTimestamp(DateTimeOffset value);

        /// <summary>
        /// Content type
        /// </summary>
        IEvent SetContentType(string? value);

        /// <summary>
        /// Content encoding
        /// </summary>
        IEvent SetContentEncoding(string? value);

        /// <summary>
        /// Add a user property
        /// </summary>
        IEvent AddProperty(string name, string? value);

        /// <summary>
        /// Whether to retain the message on the receiving end.
        /// </summary>
        IEvent SetRetain(bool value);

        /// <summary>
        /// Set quality of service
        /// </summary>
        IEvent SetQoS(QoS value);

        /// <summary>
        /// The time to live for the message
        /// </summary>
        IEvent SetTtl(TimeSpan value);

        /// <summary>
        /// Set the event schema
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        IEvent SetSchema(IEventSchema schema);

        /// <summary>
        /// Message payload buffers.
        /// </summary>
        IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value);

        /// <summary>
        /// Sends the message or messages
        /// </summary>
        /// <param name="ct">Send the event</param>
        /// <returns></returns>
        ValueTask SendAsync(CancellationToken ct = default);
    }
}
