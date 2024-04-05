// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Messaging;
    using Microsoft.Extensions.Options;
    using MQTTnet;
    using MQTTnet.Protocol;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Mqtt message
    /// </summary>
    internal sealed class MqttMessage : IEvent
    {
        /// <summary>
        /// Create message
        /// </summary>
        /// <param name="options"></param>
        /// <param name="publish"></param>
        internal MqttMessage(IOptions<MqttOptions> options,
            Func<MqttApplicationMessage, CancellationToken, ValueTask> publish)
        {
            _publish = publish;
            _version = options.Value.Protocol;
            _builder.WithQualityOfServiceLevel((MqttQualityOfServiceLevel)
                (options.Value.QoS ?? QoS.AtMostOnce));
        }

        /// <inheritdoc/>
        public IEvent SetContentEncoding(string? value)
        {
            if (_version != MqttVersion.v311 && !string.IsNullOrWhiteSpace(value))
            {
                _builder.WithUserProperty("ContentEncoding", value);
            }
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetSchema(IEventSchema schema)
        {
            _schema = schema;
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetContentType(string? value)
        {
            if (_version != MqttVersion.v311 && !string.IsNullOrWhiteSpace(value))
            {
                _builder.WithContentType(value);
            }
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetQoS(QoS value)
        {
            _builder.WithQualityOfServiceLevel((MqttQualityOfServiceLevel)value);
            return this;
        }

        /// <inheritdoc/>
        public IEvent AddProperty(string name, string? value)
        {
            if (_version != MqttVersion.v311 && !string.IsNullOrWhiteSpace(value))
            {
                _builder.WithUserProperty(name, value);
            }
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTtl(TimeSpan value)
        {
            if (_version != MqttVersion.v311)
            {
                _builder.WithMessageExpiryInterval((uint)value.TotalSeconds);
            }

            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTopic(string? value)
        {
            if (value != null)
            {
                // Check topic length.
                if (value.Length > 4096)
                {
                    var topicLength = Encoding.UTF8.GetByteCount(value);
                    const int kMaxTopicLength = 0xffff;
                    if (topicLength > kMaxTopicLength)
                    {
                        throw new ArgumentException(
                "Topic for MQTT message cannot be larger than " +
                $"{kMaxTopicLength} bytes, but current length " +
                $"is {topicLength}.", nameof(value));
                    }
                }
                _builder.WithTopic(value);
            }
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetRetain(bool value)
        {
            _builder.WithRetainFlag(value);
            return this;
        }

        /// <inheritdoc/>
        public IEvent SetTimestamp(DateTime value)
        {
            return this;
        }

        /// <inheritdoc/>
        public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
        {
            _buffers.AddRange(value);
            return this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _buffers.Clear();
        }

        /// <inheritdoc/>
        public async ValueTask SendAsync(CancellationToken ct = default)
        {
            if (_schema != null)
            {
                _builder.WithPayload(_schema.Schema);
                var schemaMessage = _builder.Build();

                //
                // TODO: Hardcode a subpath and retain for now - we also
                // need to lru cache so we are not constantly publishing
                // the schema. Future might be using rpc here.
                //
                schemaMessage.Topic += "/schema";
                schemaMessage.Retain = true;
                schemaMessage.ContentType = _schema.Type;

                await _publish.Invoke(schemaMessage, ct).ConfigureAwait(false);

                if (_schema.Id != null && _version != MqttVersion.v311)
                {
                    // Add the schema id as cloud event property
                    // TODO: Also make configurable.
                    _builder.WithUserProperty("dataschema", _schema.Id);
                }
            }
            foreach (var buffer in _buffers)
            {
                if (buffer.IsSingleSegment)
                {
                    _builder.WithPayloadSegment(buffer.First);
                }
                else
                {
                    _builder.WithPayloadSegment(buffer.ToArray());
                }
                var message = _builder.Build();
                await _publish.Invoke(message, ct).ConfigureAwait(false);
            }
        }

        private IEventSchema? _schema;
        private readonly List<ReadOnlySequence<byte>> _buffers = new();
        private readonly MqttApplicationMessageBuilder _builder = new();
        private readonly Func<MqttApplicationMessage, CancellationToken, ValueTask> _publish;
        private readonly MqttVersion _version;
    }
}
