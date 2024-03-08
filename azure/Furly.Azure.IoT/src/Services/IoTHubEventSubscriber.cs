// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Azure.IoT;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IoT Hub cloud to device event client
    /// </summary>
    public sealed class IoTHubEventSubscriber : IEventSubscriber, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "IoTHub";

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="events"></param>
        /// <param name="logger"></param>
        public IoTHubEventSubscriber(IOptions<IoTHubServiceOptions> options,
            IIoTHubEventProcessor events, ILogger<IoTHubEventSubscriber> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                string.IsNullOrEmpty(cs.HostName))
            {
                throw new InvalidConfigurationException(
                    "IoT Hub Connection string not configured.");
            }
            _processor = new Lazy<TelemetryProcessor>(
                () => new TelemetryProcessor(this, events));
            _client = IoTHubEventClient.OpenAsync(options.Value.ConnectionString);
        }

        /// <inheritdoc/>
        public ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
        {
            return ValueTask.FromResult(_processor.Value.Subscribe(topic, consumer));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_processor.IsValueCreated)
            {
                _processor.Value.Dispose();
            }
            _client.Result.Dispose();
        }

        /// <summary>
        /// Cloud to device client adapter
        /// </summary>
        private sealed class EventClientAdapter : IEventClient
        {
            /// <inheritdoc/>
            public string Name => "IoTEdge";

            /// <inheritdoc/>
            public string Identity { get; }

            /// <inheritdoc/>
            public int MaxEventPayloadSizeInBytes { get; } = 60 * 1024; // 64 KB - leave 4 kb for properties

            /// <summary>
            /// Create cloud to device event sender
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="deviceId"></param>
            /// <param name="moduleId"></param>
            public EventClientAdapter(IoTHubEventSubscriber outer,
                string deviceId, string? moduleId)
            {
                _outer = outer;
                _deviceId = deviceId;
                _moduleId = moduleId;
                Identity = HubResource.Format(null, deviceId, moduleId);
            }

            /// <inheritdoc/>
            public IEvent CreateEvent()
            {
                return new IoTHubEventClient.IoTHubEvent(_outer._client, _deviceId,
                    _moduleId, _outer._logger);
            }

            private readonly IoTHubEventSubscriber _outer;
            private readonly string _deviceId;
            private readonly string? _moduleId;
        }

        /// <summary>
        /// Registration with telemetry event processor host
        /// </summary>
        private sealed class TelemetryProcessor : IIoTHubTelemetryHandler, IDisposable
        {
            /// <summary>
            /// Create processor
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="events"></param>
            public TelemetryProcessor(IoTHubEventSubscriber outer, IIoTHubEventProcessor events)
            {
                _registration = events.Register(this);
                _outer = outer;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _subscriptions.Clear();
                _registration.Dispose();
            }

            /// <inheritdoc/>
            public async ValueTask HandleAsync(string deviceId, string? moduleId,
                string topic, ReadOnlySequence<byte> data, string contentType, string contentEncoding,
                IReadOnlyDictionary<string, string?> properties, CancellationToken ct)
            {
                IEnumerable<Task> handles;
                lock (_subscriptions)
                {
                    var responder = new EventClientAdapter(_outer, deviceId, moduleId);
                    handles = _subscriptions
                        .Where(subscription => subscription.Matches(topic))
                        .Select(subscription => subscription.Consumer.HandleAsync(
                            topic, data, contentType, properties!, responder, ct));
                }
                await Task.WhenAll(handles).ConfigureAwait(false);
            }

            /// <summary>
            /// Subscribe consumer to topic
            /// </summary>
            /// <param name="topic"></param>
            /// <param name="consumer"></param>
            /// <returns></returns>
            public IAsyncDisposable Subscribe(string topic, IEventConsumer consumer)
            {
                if (!TopicFilter.IsValid(topic))
                {
                    throw new ArgumentException("Invalid topic filter", nameof(topic));
                }
                var subscription = new Subscription(topic, consumer, this);
                lock (_subscriptions)
                {
                    _subscriptions.Add(subscription);
                }
                return subscription;
            }

            /// <summary>
            /// Subscription
            /// </summary>
            private sealed class Subscription : IAsyncDisposable
            {
                /// <summary>
                /// Consumer
                /// </summary>
                public IEventConsumer Consumer { get; }

                /// <summary>
                /// Registered target
                /// </summary>
                public string Filter { get; }

                /// <summary>
                /// Create subscription
                /// </summary>
                /// <param name="filter"></param>
                /// <param name="consumer"></param>
                /// <param name="outer"></param>
                public Subscription(string filter, IEventConsumer consumer,
                    TelemetryProcessor outer)
                {
                    Filter = filter;
                    Consumer = consumer;
                    _outer = outer;
                }

                /// <summary>
                /// Returns true if the target matches the subscription.
                /// </summary>
                /// <param name="topic"></param>
                /// <returns></returns>
                public bool Matches(string topic)
                {
                    return TopicFilter.Matches(topic, Filter);
                }

                /// <inheritdoc/>
                public ValueTask DisposeAsync()
                {
                    lock (_outer._subscriptions)
                    {
                        _outer._subscriptions.Remove(this);
                    }
                    return ValueTask.CompletedTask;
                }

                private readonly TelemetryProcessor _outer;
            }

            private readonly IDisposable _registration;
            private readonly List<Subscription> _subscriptions = new();
            private readonly IoTHubEventSubscriber _outer;
        }

        private readonly Task<ServiceClient> _client;
        private readonly Lazy<TelemetryProcessor> _processor;
        private readonly ILogger _logger;
    }
}
