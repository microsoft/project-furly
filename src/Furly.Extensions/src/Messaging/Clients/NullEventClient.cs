// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging.Clients
{
    using Furly.Extensions.Messaging;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Nil output client
    /// </summary>
    public sealed class NullEventClient : IEventClient, IEvent
    {
        /// <inheritdoc/>
        public string Name => "NULL";
        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => int.MaxValue;
        /// <inheritdoc/>
        public string Identity => Dns.GetHostName();

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return this;
        }

        /// <inheritdoc/>
        public ValueTask SendAsync(CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public IEvent SetQoS(QoS value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent AddProperty(string name, string? value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetContentEncoding(string? value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetSchema(string name, ulong version,
            ReadOnlyMemory<byte> schema, string contentType)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetContentType(string? value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetRetain(bool value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTimestamp(DateTime value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTopic(string? value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTtl(TimeSpan value)
        {
            return this;
        }
    }
}
