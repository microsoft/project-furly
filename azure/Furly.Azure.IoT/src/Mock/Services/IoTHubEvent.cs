// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock.Services
{
    using Furly.Extensions.Messaging;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IoT Hub Event messages
    /// </summary>
    public sealed record class IoTHubEvent : IEvent
    {
        /// <summary>
        /// Copy constructor
        /// </summary>
        internal IoTHubEvent()
        {
            DeviceId = null!;
        }

        /// <summary>
        /// Create event
        /// </summary>
        /// <param name="send"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        internal IoTHubEvent(Action<IoTHubEvent> send,
            string deviceId, string? moduleId)
        {
            DeviceId = deviceId;
            ModuleId = moduleId;
            _send = send;
        }

        /// <inheritdoc/>
        public CloudEventHeader? Ce { get; private set; }

        /// <inheritdoc/>
        public IEvent AsCloudEvent(CloudEventHeader header)
        {
            Ce = header;
            return this;
        }

        /// <inheritdoc/>
        public QoS QoS { get; private set; }

        /// <inheritdoc/>
        public IEvent SetQoS(QoS value)
        {
            QoS = value;
            return this;
        }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; private set; }

        /// <inheritdoc/>
        public IEvent SetTimestamp(DateTimeOffset value)
        {
            Timestamp = value;
            return this;
        }

        /// <inheritdoc/>
        public string? ContentType { get; private set; }

        /// <inheritdoc/>
        public IEvent SetContentType(string? value)
        {
            ContentType = value;
            return this;
        }

        /// <inheritdoc/>
        public string? ContentEncoding { get; private set; }

        /// <inheritdoc/>
        public IEvent SetContentEncoding(string? value)
        {
            ContentEncoding = value;
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetSchema(IEventSchema schema)
        {
            return this;
        }

        /// <inheritdoc/>
        public string DeviceId { get; set; }

        /// <inheritdoc/>
        public string? ModuleId { get; set; }

        /// <inheritdoc/>
        public string? Topic { get; private set; }

        /// <inheritdoc/>
        public IEvent SetTopic(string? value)
        {
            Topic = value;
            return this;
        }

        /// <inheritdoc/>
        public bool Retain { get; private set; }

        /// <inheritdoc/>
        public IEvent SetRetain(bool value)
        {
            Retain = value;
            return this;
        }

        /// <inheritdoc/>
        public TimeSpan Ttl { get; private set; }

        /// <inheritdoc/>
        public IEvent SetTtl(TimeSpan value)
        {
            Ttl = value;
            return this;
        }

        /// <inheritdoc/>
        internal Dictionary<string, string?> Properties { get; } = [];

        /// <inheritdoc/>
        public IEvent AddProperty(string name, string? value)
        {
            Properties.AddOrUpdate(name, value);
            return this;
        }

        /// <inheritdoc/>
        internal List<ReadOnlySequence<byte>> Buffers { get; } = [];

        /// <inheritdoc/>
        public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
        {
            Buffers.AddRange(value);
            return this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public ValueTask SendAsync(CancellationToken ct = default)
        {
            _send?.Invoke(this);
            return ValueTask.CompletedTask;
        }

        private readonly Action<IoTHubEvent>? _send;
    }
}
