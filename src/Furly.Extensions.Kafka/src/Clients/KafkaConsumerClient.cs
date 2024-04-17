// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Autofac;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of event processor host interface to host event
    /// processors.
    /// </summary>
    public sealed class KafkaConsumerClient : IEventSubscriber, IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "Kafka";

        /// <summary>
        /// Create host
        /// </summary>
        /// <param name="admin"></param>
        /// <param name="server"></param>
        /// <param name="config"></param>
        /// <param name="identity"></param>
        /// <param name="logger"></param>
        public KafkaConsumerClient(IKafkaAdminClient admin,
            IOptions<KafkaServerOptions> server, IOptions<KafkaConsumerOptions> config,
            IProcessIdentity identity, ILogger<KafkaConsumerClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _consumerId = identity.Identity ?? Guid.NewGuid().ToString();
            _interval = (int?)config.Value.CheckpointInterval?.TotalMilliseconds;
            _runner = Task.Factory.StartNew(() => RunAsync(_cts.Token), _cts.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <inheritdoc/>
        public ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
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
            return ValueTask.FromResult<IAsyncDisposable>(subscription);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_runner == null)
                {
                    return;
                }
                await _cts.CancelAsync().ConfigureAwait(false);
                await _runner.ConfigureAwait(false);
            }
            catch { }
            finally
            {
                _cts.Dispose();
            }
        }

        /// <inheritdoc/>
        private async Task RunAsync(CancellationToken ct)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _server.Value.BootstrapServers,
                GroupId = _config.Value.ConsumerGroup,
                AutoOffsetReset = _config.Value.InitialReadFromEnd ?
                    AutoOffsetReset.Latest : AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                EnableAutoOffsetStore = true,
                AutoCommitIntervalMs = _interval,
                // ...
            };
            var consumerTopic = _config.Value.ConsumerTopic ?? "^.*";
            if (!consumerTopic.StartsWith('^'))
            {
                await _admin.EnsureTopicExistsAsync(consumerTopic).ConfigureAwait(false);
            }
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var consumer = new ConsumerBuilder<string, byte[]>(config)
                        .SetErrorHandler(OnError)
                        .SetStatisticsHandler(OnMetrics)
                        .SetLogHandler((_, m) => _logger.Log(m))
                        .Build())
                    {
                        _logger.LogInformation("Starting consumer {ConsumerId} on {Topic}...",
                            _consumerId, consumerTopic);
                        consumer.Subscribe(consumerTopic);
                        while (!ct.IsCancellationRequested)
                        {
                            var result = consumer.Consume(ct);
                            var ev = result.Message;
                            if (result.Topic == "__consumer_offsets")
                            {
                                continue;
                            }
                            if (_config.Value.SkipEventsOlderThan != null &&
                                ev.Timestamp.UtcDateTime +
                                    _config.Value.SkipEventsOlderThan < DateTime.UtcNow)
                            {
                                // Skip this one and catch up
                                continue;
                            }
                            if (!TryGetValue(ev.Headers, "ContentType", out var contentType) ||
                                !TryGetValue(ev.Headers, "Topic", out var topic))
                            {
                                continue;
                            }

                            // TODO: Add a wrapper interface over the list
                            var properties = new Dictionary<string, string?>();
                            foreach (var property in ev.Headers)
                            {
                                if (TryGetValue(ev.Headers, property.Key, out var value))
                                {
                                    properties.AddOrUpdate(property.Key, value);
                                }
                                else if (!properties.ContainsKey(property.Key))
                                {
                                    properties.Add(property.Key, null);
                                }
                            }

                            IEnumerable<Task> handles;
                            lock (_subscriptions)
                            {
                                handles = _subscriptions
                                    .Where(subscription => subscription.Matches(topic))
                                    .Select(subscription => subscription.Consumer.HandleAsync(
                                        topic, new ReadOnlySequence<byte>(ev.Value), contentType,
                                        properties, null, ct));
                            }
                            await Task.WhenAll(handles).ConfigureAwait(false);

                            static bool TryGetValue(Headers headers, string key,
                                [NotNullWhen(true)] out string? value)
                            {
                                if (headers.TryGetLastBytes(key, out var result))
                                {
                                    value = Encoding.UTF8.GetString(result);
                                    return true;
                                }
                                value = null;
                                return false;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception error)
                {
                    // Exception - report and continue
                    _logger.LogWarning(error, "Consumer {ConsumerId} encountered error...",
                        _consumerId);
                }
            }
            _logger.LogInformation("Exiting consumer {ConsumerId} on {Topic}...",
                _consumerId, consumerTopic);
        }

        /// <summary>
        /// Handle error
        /// </summary>
        /// <param name="client"></param>
        /// <param name="error"></param>
        private void OnError(IConsumer<string, byte[]> client, Error error)
        {
            // Todo
        }

        /// <summary>
        /// Handle metrics
        /// </summary>
        /// <param name="client"></param>
        /// <param name="json"></param>
        private void OnMetrics(IConsumer<string, byte[]> client, string json)
        {
            // Todo
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
            /// <param name="topic"></param>
            /// <param name="consumer"></param>
            /// <param name="outer"></param>
            public Subscription(string topic, IEventConsumer consumer,
                KafkaConsumerClient outer)
            {
                Filter = topic;
                Consumer = consumer;
                _outer = outer;
            }

            /// <summary>
            /// Returns true if the source matches the subscription topic.
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

            private readonly KafkaConsumerClient _outer;
        }

        private readonly List<Subscription> _subscriptions = new();
        private readonly ILogger _logger;
        private readonly IKafkaAdminClient _admin;
        private readonly IOptions<KafkaConsumerOptions> _config;
        private readonly IOptions<KafkaServerOptions> _server;
        private readonly string _consumerId;
        private readonly int? _interval;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _runner;
    }
}
