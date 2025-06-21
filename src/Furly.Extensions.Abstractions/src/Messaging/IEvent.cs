// ------------------------------------------------------------
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
        /// Timestamp of when the occurrence happened. If the time of
        /// the occurrence cannot be determined then this attribute MAY
        /// be set to some other time (such as the current time) by the
        /// CloudEvents producer, however all producers for the same
        /// source MUST be consistent in this respect. In other words,
        /// either they all use the actual time of the occurrence or
        /// they all use the same algorithm to determine the value used.
        /// </summary>
        IEvent SetTimestamp(DateTimeOffset value);

        /// <summary>
        /// Content type for the protocol. This is not the cloud events
        /// content type header which must be set through the cloud event
        /// call if desired.
        /// </summary>
        IEvent SetContentType(string? value);

        /// <summary>
        /// Content encoding
        /// </summary>
        IEvent SetContentEncoding(string? value);

        /// <summary>
        /// <para>
        /// Send as cloud event. If called the event will set the header
        /// information according to the protocol binding of cloud events.
        /// </para>
        /// <para>
        /// It will also add the dataschema property as defined in the
        /// cloud event schema from the schema.
        /// </para>
        /// </summary>
        /// <param name="header"></param>
        /// <returns></returns>
        IEvent AsCloudEvent(CloudEventHeader header);

        /// <summary>
        /// Set the event schema for this event. This will register the
        /// schema with a schema registry and then add the resulting schema
        /// reference as dataschema cloud event header to the event.
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        IEvent SetSchema(IEventSchema schema);

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
