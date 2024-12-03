// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using Furly.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Exceptions;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Rabbitmq connection
    /// </summary>
    public sealed class RabbitMqConnection : IRabbitMqConnection, IDisposable
    {
        /// <inheritdoc/>
        public int MaxMessageSizeInBytes =>
            _options.Value.MessageMaxBytes ?? 512 * 1024 * 1024;

        /// <inheritdoc/>
        public string Name =>
            _options.Value.HostName ?? string.Empty;

        /// <summary>
        /// Create connection
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public RabbitMqConnection(IOptionsSnapshot<RabbitMqOptions> options,
            ILogger<RabbitMqConnection> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public Task<IRabbitMqChannel> GetTopicChannelAsync(
            string? topic, IRabbitMqConsumer? consumer)
        {
            return RabbitMqChannel.CreateAsync(this, topic, true, consumer);
        }

        /// <inheritdoc/>
        public Task<IRabbitMqChannel> GetQueueChannelAsync(
            string queue, IRabbitMqConsumer? consumer)
        {
            return RabbitMqChannel.CreateAsync(this, queue, false, consumer);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _connection?.Dispose();
            _lock.Dispose();
        }

        /// <summary>
        /// Try to create connection
        /// </summary>
        /// <returns></returns>
        private async Task<IConnection> GetConnectionAsync(bool forceReconnect = false)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_connection == null || forceReconnect)
                {
                    _connection?.Dispose();
                    _connection = await CreateConnectionAsync().ConfigureAwait(false);
                }
                return _connection;
            }
            finally
            {
                _lock.Release();
            }

            // create connection with retry
            async Task<IConnection> CreateConnectionAsync()
            {
                var attempts = 0;
                while (true)
                {
                    try
                    {
                        return new ConnectionFactory
                        {
                            HostName = _options.Value.HostName,
                            Password = _options.Value.Key,
                            UserName = _options.Value.UserName,

                            AutomaticRecoveryEnabled = true,
                            NetworkRecoveryInterval = TimeSpan.FromMilliseconds(500),
                            DispatchConsumersAsync = true
                        }.CreateConnection();
                    }
                    catch (BrokerUnreachableException bue) when (attempts++ < 60)
                    {
                        var waitBeforeReconnecting = TimeSpan.FromSeconds(attempts);
                        _logger.LogInformation(
                            "Failed to connect due to {Message}. Trying again in {Wait}...",
                            bue.Message, waitBeforeReconnecting);
                        await Task.Delay(waitBeforeReconnecting).ConfigureAwait(false);
                        _logger.LogInformation("Trying to connect to broker at {Host}...",
                            _options.Value.HostName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to connect to broker at {Host}. Give up.",
                            _options.Value.HostName);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Channel
        /// </summary>
        private class RabbitMqChannel : IRabbitMqChannel
        {
            /// <inheritdoc/>
            public string QueueName { get; private set; } = string.Empty;

            /// <inheritdoc/>
            public string ExchangeName { get; }

            /// <summary>
            /// Create channel
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="routingKey"></param>
            /// <param name="pubSub"></param>
            /// <param name="consumer"></param>
            private RabbitMqChannel(RabbitMqConnection outer,
                string? routingKey, bool pubSub, IRabbitMqConsumer? consumer)
            {
                _logger = outer._logger;
                _scope = _logger.BeginScope("{ChannelName}", new
                {
                    ChannelName = routingKey
                });
                _outer = outer;
                _routingKey = routingKey;
                _pubSub = pubSub;
                _consumer = consumer;

                ExchangeName = _outer._options.Value.Exchange ?? string.Empty;
                if (_pubSub && string.IsNullOrEmpty(ExchangeName))
                {
                    // default exchange is not allowed in topics.
                    ExchangeName = "furly";
                }
                _channel = CreateChannelAsync();
            }

            /// <summary>
            /// Create a channel - wait until it is created
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="routingKey"></param>
            /// <param name="pubSub"></param>
            /// <param name="consumer"></param>
            /// <returns></returns>
            internal static async Task<IRabbitMqChannel> CreateAsync(RabbitMqConnection outer,
                string? routingKey, bool pubSub, IRabbitMqConsumer? consumer)
            {
                var channel = new RabbitMqChannel(outer, routingKey, pubSub, consumer);
                await channel._channel.ConfigureAwait(false);
                return channel;
            }

            /// <inheritdoc/>
            public IEvent CreateEvent(bool mandatory)
            {
                return new RabbitMqEvent(this, mandatory);
            }

            public void Dispose()
            {
                CloseChannel();
                _isDisposed = true;
                _scope?.Dispose();
            }

            /// <summary>
            /// Create model
            /// </summary>
            /// <returns></returns>
            private async Task<Channel> CreateChannelAsync()
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        var connection = await _outer.GetConnectionAsync(attempt != 1).ConfigureAwait(false);
                        return CreateChannelInternal(connection);
                    }
                    catch (OperationInterruptedException ex) when (attempt < 60)
                    {
                        _logger.LogError(ex, "Failed to open channel {Attempt}", attempt);
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
            }

            /// <summary>
            /// Create channel
            /// </summary>
            /// <returns></returns>
            private Channel CreateChannelInternal(IConnection connection)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                try
                {
                    var model = connection.CreateModel();
                    if (_consumer != null)
                    {
                        // Consumer queues
                        if (_pubSub)
                        {
                            // Create queue and exchange
                            QueueName = model.QueueDeclare().QueueName;

                            // Create exchange and bind queue to it
                            model.ExchangeDeclare(ExchangeName, ExchangeType.Topic, true);
                            model.QueueBind(QueueName, ExchangeName, _routingKey);
                        }
                        else
                        {
                            // Create Queue
                            QueueName = model.QueueDeclare(_routingKey, true, false).QueueName;
                            if (!string.IsNullOrEmpty(ExchangeName))
                            {
                                model.ExchangeDeclare(ExchangeName, ExchangeType.Direct, true);
                            }
                        }

                        // Channel creation sets up consumption
                    }
                    else
                    {
                        // Publisher queue
                        if (_pubSub)
                        {
                            // Create exchange
                            QueueName = string.Empty; // default
                            model.ExchangeDeclare(ExchangeName, ExchangeType.Topic, true);
                        }
                        else
                        {
                            // Create Queue
                            QueueName = model.QueueDeclare(_routingKey, true, false).QueueName;
                            if (!string.IsNullOrEmpty(ExchangeName))
                            {
                                model.ExchangeDeclare(ExchangeName, ExchangeType.Direct, true);
                            }
                        }

                        model.ConfirmSelect();
                        model.BasicAcks += (sender, ea) =>
                            HandleConfirm(ea.Multiple, ea.DeliveryTag, () => null);
                        model.BasicNacks += (sender, ea) =>
                            HandleConfirm(ea.Multiple, ea.DeliveryTag,
                                () => new InvalidOperationException("Failed sending"));
                    }
                    return new Channel(model, this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create channel");
                    throw;
                }
            }

            /// <summary>
            /// Handle confirmation
            /// </summary>
            /// <param name="multiple"></param>
            /// <param name="sequenceNumber"></param>
            /// <param name="ex"></param>
            private void HandleConfirm(bool multiple, ulong sequenceNumber,
                Func<Exception?> ex)
            {
                if (!multiple)
                {
                    if (_completions.TryRemove(sequenceNumber, out var a))
                    {
                        Try.Op(() => a.Invoke(ex()));
                    }
                }
                else
                {
                    foreach (var entry in _completions.Where(k => k.Key <= sequenceNumber))
                    {
                        if (_completions.TryRemove(entry.Key, out var a))
                        {
                            Try.Op(() => a.Invoke(ex()));
                        }
                    }
                }
            }

            /// <summary>
            /// Close channel
            /// </summary>
            private void CloseChannel()
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                while (_consumer == null && !_completions.IsEmpty)
                {
                    foreach (var entry in _completions.ToList())
                    {
                        if (_completions.TryRemove(entry.Key, out var a))
                        {
                            Try.Op(() => a.Invoke(
                                new InvalidOperationException("Closed")));
                        }
                    }
                }
                _channel.Result.Dispose();
            }

            /// <summary>
            /// Event wrapper
            /// </summary>
            private sealed class RabbitMqEvent : IEvent
            {
                /// <summary>
                /// Create event
                /// </summary>
                /// <param name="outer"></param>
                /// <param name="mandatory"></param>
                public RabbitMqEvent(RabbitMqChannel outer, bool mandatory)
                {
                    _outer = outer;
                    _mandatory = mandatory;
                    _routingKey = _outer._routingKey;
                    _properties = _outer._channel.Result.Model.CreateBasicProperties();
                }

                /// <inheritdoc/>
                public IEvent SetQoS(QoS value)
                {
                    // TODO: Set prefetch count
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetTopic(string? value)
                {
                    if (_outer._pubSub)
                    {
                        if (value != null)
                        {
                            _routingKey = value.Replace('/', '.');
                        }
                        else
                        {
                            _routingKey = _outer._routingKey;
                        }
                    }
                    else // In case of queue do not route using topic
                    {
                        if (value == null)
                        {
                            _properties.ClearType();
                        }
                        else
                        {
                            _properties.Type = value;
                        }
                    }
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetTimestamp(DateTimeOffset value)
                {
                    _properties.Timestamp
                        = new AmqpTimestamp(value.UtcDateTime.ToFileTimeUtc());
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetContentType(string? value)
                {
                    if (value == null)
                    {
                        _properties.ClearContentType();
                    }
                    else
                    {
                        _properties.ContentType = value;
                    }
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetContentEncoding(string? value)
                {
                    if (value == null)
                    {
                        _properties.ClearContentEncoding();
                    }
                    else
                    {
                        _properties.ContentEncoding = value;
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
                    if (value == null)
                    {
                        // TODO: _properties[name] = value;
                    }
                    else
                    {
                        // TODO: _properties.AppId = value;
                    }
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetRetain(bool value)
                {
                    _properties.DeliveryMode = (byte)(value ? 2 : 1);
                    return this;
                }

                /// <inheritdoc/>
                public IEvent SetTtl(TimeSpan value)
                {
                    _properties.Expiration = value.ToString();
                    return this;
                }

                /// <inheritdoc/>
                public IEvent AddBuffers(IEnumerable<ReadOnlySequence<byte>> value)
                {
                    _buffers.AddRange(value);
                    return this;
                }

                /// <inheritdoc/>
                public async ValueTask SendAsync(CancellationToken ct)
                {
                    if (_buffers.Count == 0)
                    {
                        return;
                    }
                    try
                    {
                        var tcs = new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        var channel = await _outer._channel.ConfigureAwait(false);
                        Publish(channel, tcs, (t, ex) =>
                        {
                            if (ex != null)
                            {
                                t.SetException(ex);
                            }
                            else
                            {
                                _outer._logger.LogTrace(
                                    "-----> Messages published to {Channel}...",
                                    _routingKey);
                                t.SetResult(true);
                            }
                        });
                        await tcs.Task.ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _outer._logger.LogError(ex,
                            "Failed to publish message to channel {Channel}",
                            _routingKey);
                        throw;
                    }
                }

                /// <summary>
                /// Publish with callback
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="channel"></param>
                /// <param name="token"></param>
                /// <param name="complete"></param>
                /// <exception cref="ResourceInvalidStateException"></exception>
                private void Publish<T>(Channel channel, T token, Action<T, Exception?> complete)
                {
                    System.Diagnostics.Debug.Assert(_buffers.Count > 0);
                    lock (_outer._channel)
                    {
                        if (_buffers.Count == 1)
                        {
                            var seq = channel.Model.NextPublishSeqNo;
                            if (!_outer._completions.TryAdd(seq, ex => complete(token, ex)))
                            {
                                throw new ResourceInvalidStateException(
                                    "sequence number in use");
                            }
                            channel.Model.BasicPublish(_outer.ExchangeName, _routingKey,
                                _mandatory, _properties, _buffers[0].IsSingleSegment
                                    ? _buffers[0].First : _buffers[0].ToArray());
                        }
                        else
                        {
                            var lastSeq = channel.Model.NextPublishSeqNo
                                + (ulong)_buffers.Count - 1;
                            if (!_outer._completions.TryAdd(lastSeq, ex => complete(token, ex)))
                            {
                                throw new ResourceInvalidStateException(
                                    "sequence number in use");
                            }
                            var bulk = channel.Model.CreateBasicPublishBatch();
                            foreach (var body in _buffers)
                            {
                                bulk.Add(_outer.ExchangeName, _routingKey, _mandatory,
                                    _properties, body.IsSingleSegment
                                        ? body.First : body.ToArray());
                            }
                            bulk.Publish();
                        }
                    }
                }

                /// <inheritdoc/>
                public void Dispose()
                {
                    _buffers.Clear();
                }

                private string? _routingKey;
                private readonly List<ReadOnlySequence<byte>> _buffers = [];
                private readonly RabbitMqChannel _outer;
                private readonly bool _mandatory;
                private readonly IBasicProperties _properties;
            }

            /// <summary>
            /// Internal channel object
            /// </summary>
            private class Channel : AsyncDefaultBasicConsumer, IDisposable
            {
                /// <inheritdoc/>
                public Channel(IModel model, RabbitMqChannel outer) :
                    base(model)
                {
                    _outer = outer;
                    _logger = outer._logger;
                    _scope = _logger.BeginScope(new
                    {
                        outer.QueueName,
                        outer.ExchangeName,
                        ConsumerTag = _consumerTag
                    });
                    if (_outer._consumer != null)
                    {
                        // Start consume
                        _logger.LogInformation("Starting to consume...");
                        model.BasicConsume(_outer.QueueName, true, this);
                    }
                }

                /// <inheritdoc/>
                public override Task HandleBasicDeliver(string consumerTag,
                    ulong deliveryTag, bool redelivered, string exchange,
                    string routingKey, IBasicProperties properties,
                    ReadOnlyMemory<byte> body)
                {
                    return _outer._consumer!.HandleBasicDeliver(Model, deliveryTag,
                        redelivered, exchange, routingKey, properties, body);
                }

                /// <inheritdoc/>
                public override Task HandleModelShutdown(object model,
                    ShutdownEventArgs reason)
                {
                    _logger.LogInformation("Channel shutdown by {Initiator}.",
                        reason.Initiator);
                    if (reason.Initiator == ShutdownInitiator.Peer)
                    {
                        // TODO - restart
                    }
                    return base.HandleModelShutdown(model, reason);
                }

                /// <inheritdoc/>
                public override Task HandleBasicConsumeOk(string consumerTag)
                {
                    // Consuming
                    _logger.LogInformation("Consumer {Tag} started.", consumerTag);
                    return base.HandleBasicConsumeOk(consumerTag);
                }

                /// <inheritdoc/>
                public void Dispose()
                {
                    try
                    {
                        if (Model.IsClosed)
                        {
                            return;
                        }
                        if (IsRunning)
                        {
                            // Stop consume
                            Model.BasicCancelNoWait(_consumerTag);
                        }
                        Model.Close();
                    }
                    finally
                    {
                        Model.Dispose();
                        _scope?.Dispose();
                    }
                }

                private readonly ILogger _logger;
                private readonly IDisposable? _scope;
                private readonly RabbitMqChannel _outer;
                private readonly string _consumerTag = Guid.NewGuid().ToString();
            }

            private readonly Task<Channel> _channel;
            private bool _isDisposed;
            private readonly string? _routingKey;
            private readonly bool _pubSub;
            private readonly ILogger _logger;
            private readonly IDisposable? _scope;
            private readonly IRabbitMqConsumer? _consumer;
            private readonly RabbitMqConnection _outer;
            private readonly ConcurrentDictionary<ulong, Action<Exception?>> _completions = new();

            private class NullConsumer : IRabbitMqConsumer
            {
                public static readonly IRabbitMqConsumer Instance = new NullConsumer();

                /// <inheritdoc/>
                public Task HandleBasicDeliver(IModel model, ulong deliveryTag,
                    bool redelivered, string exchange, string routingKey,
                    IBasicProperties properties, ReadOnlyMemory<byte> body)
                {
                    return Task.CompletedTask;
                }
            }
        }

        private IConnection? _connection;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger _logger;
        private readonly IOptionsSnapshot<RabbitMqOptions> _options;
    }
}
