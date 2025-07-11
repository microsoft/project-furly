// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Azure.IoT;
    using Furly.Azure;
    using Furly.Exceptions;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IoT Hub cloud to device event client
    /// </summary>
    public sealed class IoTHubEventClient : IEventClient, IDisposable, IProcessIdentity
    {
        /// <inheritdoc/>
        public string Name => "IoTHub";

        /// <inheritdoc/>
        public string Identity { get; }

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes { get; } = 60 * 1024; // 64 KB - leave 4 kb for properties

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="device"></param>
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        public IoTHubEventClient(IOptions<IoTHubServiceOptions> options,
            IOptions<IoTHubDeviceOptions> device, ICredentialProvider credential,
            ILogger<IoTHubEventClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                string.IsNullOrEmpty(cs.HostName))
            {
                throw new InvalidConfigurationException(
                    "IoT Hub Connection string not configured.");
            }
            _deviceId = device.Value.DeviceId ?? throw new InvalidConfigurationException(
                    "IoT Hub Device id string not configured.");
            _moduleId = device.Value.ModuleId;
            Identity = HubResource.Format(cs.HostName, _deviceId, _moduleId);
            _client = OpenAsync(cs, credential);
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return new IoTHubEvent(_client, _deviceId, _moduleId, _logger);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.Result.Dispose();
        }

        /// <summary>
        /// Open service client
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="credential"></param>
        /// <returns></returns>
        internal static async Task<ServiceClient> OpenAsync(ConnectionString connectionString,
            ICredentialProvider credential)
        {
            var client = CreateServiceClient(connectionString);
            await client.OpenAsync().ConfigureAwait(false);
            return client;

            ServiceClient CreateServiceClient(ConnectionString connectionString)
            {
                Debug.Assert(!string.IsNullOrEmpty(connectionString.HostName));
                if (string.IsNullOrEmpty(connectionString.SharedAccessKey) ||
                    string.IsNullOrEmpty(connectionString.SharedAccessKeyName))
                {
                    return ServiceClient.Create(connectionString.HostName, credential.Credential);
                }
                else
                {
                    return ServiceClient.CreateFromConnectionString(connectionString.ToString());
                }
            }
        }
        internal sealed class IoTHubEvent : IEvent
        {
            /// <summary>
            /// Create event
            /// </summary>
            /// <param name="client"></param>
            /// <param name="deviceId"></param>
            /// <param name="moduleId"></param>
            /// <param name="logger"></param>
            public IoTHubEvent(Task<ServiceClient> client,
                string deviceId, string? moduleId, ILogger logger)
            {
                _client = client;
                _deviceId = deviceId;
                _moduleId = moduleId;
                _logger = logger;
            }

            /// <inheritdoc/>
            public IEvent AsCloudEvent(CloudEventHeader header)
            {
                _properties.AddOrUpdate("specversion", "1.0");
                _properties.AddOrUpdate("id", header.Id);
                _properties.AddOrUpdate("source", header.Source.ToString());
                _properties.AddOrUpdate("type", header.Type);
                if (header.Time != null)
                {
                    _properties.AddOrUpdate("time", header.Time.ToString());
                }
                if (header.DataContentType != null)
                {
                    _properties.AddOrUpdate("datacontenttype", header.DataContentType);
                }
                if (header.Subject != null)
                {
                    _properties.AddOrUpdate("subject", header.Subject);
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _contentType = value;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _contentEncoding = value;
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetSchema(IEventSchema schema)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                _properties.AddOrUpdate(name, value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
            {
                _buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                _topic = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTimeOffset value)
            {
                return this;
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct)
            {
                if (_buffers.Count == 0)
                {
                    return;
                }
                var messages = _buffers.ConvertAll(m => CreateMessage(m.ToArray()));
                try
                {
                    foreach (var msg in messages)
                    {
                        var client = await _client.ConfigureAwait(false);
                        await (string.IsNullOrEmpty(_moduleId) ?
                             client.SendAsync(_deviceId, msg) :
                             client.SendAsync(_deviceId, _moduleId, msg)).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    _logger.SendingMessageFailed(e, _deviceId, _moduleId ?? string.Empty);
                    throw e.Translate();
                }
                finally
                {
                    foreach (var hubMessage in messages)
                    {
                        hubMessage.Dispose();
                    }
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _buffers.Clear();
            }

            /// <summary>
            /// Build message
            /// </summary>
            private Message CreateMessage(byte[] buffer)
            {
                var message = new Message(buffer)
                {
                    ContentEncoding = _contentEncoding,
                    ContentType = _contentType,
                    MessageSchema = _topic
                };
                foreach (var item in _properties)
                {
                    message.Properties.AddOrUpdate(item.Key, item.Value);
                }
                return message;
            }

            private readonly Dictionary<string, string?> _properties = [];
            private readonly List<ReadOnlySequence<byte>> _buffers = [];
            private readonly Task<ServiceClient> _client;
            private readonly string _deviceId;
            private readonly string? _moduleId;
            private readonly ILogger _logger;
            private string? _topic;
            private string? _contentEncoding;
            private string? _contentType;
        }

        private readonly string _deviceId;
        private readonly string? _moduleId;
        private readonly Task<ServiceClient> _client;
        private readonly ILogger _logger;
    }

    /// <summary>
    /// Source-generated logging for IoTHubEventClient
    /// </summary>
    internal static partial class IoTHubEventClientLogging
    {
        private const int EventClass = 0;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Trace,
            Message = "Sending message to {DeviceId} ({ModuleId}) failed.")]
        public static partial void SendingMessageFailed(this ILogger logger,
            Exception ex, string deviceId, string moduleId);
    }
}
