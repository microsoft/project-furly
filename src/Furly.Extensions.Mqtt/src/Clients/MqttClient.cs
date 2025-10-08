// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Metrics;
    using Furly.Extensions.Serializers;
    using Furly.Exceptions;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MQTTnet;
    using MQTTnet.Exceptions;
    using MQTTnet.Formatter;
    using MQTTnet.Packets;
    using MQTTnet.Protocol;
    using Nito.AsyncEx;
    using Nito.Disposables;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Mqtt event client
    /// </summary>
    public sealed class MqttClient : MqttRpcBase, IEventClient, IEventSubscriber,
        IMqttPublish, IManagedClient, IDisposable, IAwaitable<MqttClient>
    {
        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes => MaxMethodPayloadSizeInBytes;

        /// <inheritdoc/>
        public string Identity { get; }

        /// <inheritdoc/>
        public string? ClientId => Identity;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion { get; }

        /// <inheritdoc/>
        public Func<MqttMessageReceivedEventArgs, Task>? MessageReceived { get; set; }

        /// <summary>
        /// Create mqtt client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="serializer"></param>
        /// <param name="registry"></param>
        /// <param name="meter"></param>
        public MqttClient(IOptions<MqttOptions> options, ILogger<MqttClient> logger,
            ISerializer serializer, ISchemaRegistry? registry = null,
            IMeterProvider? meter = null) : base(options, serializer, logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _meterProvider ??= MeterProvider.Default;
            _metrics = new Metrics(_meterProvider.Meter);

            Identity = _options.Value.ClientId ?? Guid.NewGuid().ToString();
            ProtocolVersion = _options.Value.Protocol == MqttVersion.v5 ?
                MqttProtocolVersion.V500 : MqttProtocolVersion.V311;
            var numberofPartitions = _options.Value.NumberOfClientPartitions ?? 0;

            _sessions = Enumerable
                .Range(0, numberofPartitions == 0 ? 1 : numberofPartitions)
                .Select(_ => CreateSession())
                .ToArray();

            _cts = new CancellationTokenSource();
            _publisher = _options.Value.ConfigureSchemaMessage == null && registry == null
                ? this : new MqttSchemaPublisher(_options, this, registry);
            _connection = Task.WhenAll(_sessions
                .Select(c => c.ConnectAsync(GetSessionClientOptions(), _cts.Token)));
            _subscriber = Task.Factory.StartNew(() => SubscribeAsync(_cts.Token),
                _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <inheritdoc/>
        public IAwaiter<MqttClient> GetAwaiter()
        {
            return _connection.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            var clientId = _options.Value.ClientId ?? "unknown";
            _logger.ClientClosing(clientId);

            // Dispose base server
            try
            {
                await base.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.RpcServerStopFailed(ex);
            }

            // Stop subscriber
            try
            {
                await _cts.CancelAsync().ConfigureAwait(false);
                await _subscriber.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.SubscriberStopFailed(ex);
            }

            // Stop session clients
            foreach (var client in _sessions)
            {
                var sessionId = client.ClientId ?? "unknown";
                try
                {
                    // Give up after 30 seconds
                    await client.DisposeAsync().AsTask()
                        .WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    _logger.SessionClosed(clientId, sessionId);
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    _logger.SessionCloseFailed(ex, clientId, sessionId);
                }
            }

            _subscriptionsLock.Dispose();
            _cts.Dispose();
            _metrics.Dispose();
            _isDisposed = true;
            _logger.ClientClosed(clientId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<MqttClientPublishResult> PublishAsync(
            MqttApplicationMessage message, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();
            var client = await GetSessionAsync(message.Topic, ct).ConfigureAwait(false);
            return await client.PublishAsync(message, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MqttClientSubscribeResult> SubscribeAsync(
            MqttClientSubscribeOptions options, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            System.Diagnostics.Debug.Assert(_sessions.Length != 0);

            if (options.TopicFilters == null || options.TopicFilters.Count == 0)
            {
                throw new ArgumentException("No topic filters specified", nameof(options));
            }
            if (!_connection.IsCompleted)
            {
                await _connection.WaitAsync(ct).ConfigureAwait(false);
            }

            if (_sessions.Length == 1)
            {
                return await _sessions[0].SubscribeAsync(options, ct).ConfigureAwait(false);
            }
            if (options.TopicFilters.Count == 1)
            {
                return await GetSessionForTopic(options.TopicFilters[0].Topic)
                    .SubscribeAsync(options, ct).ConfigureAwait(false);
            }

            var subscriptions = options.TopicFilters
                .GroupBy(topic => GetSessionIndex(topic.Topic))
                .Select(group => _sessions[group.Key].SubscribeAsync(
                    new MqttClientSubscribeOptions
                    {
                        SubscriptionIdentifier = options.SubscriptionIdentifier,
                        UserProperties = options.UserProperties,
                        TopicFilters = [.. group]
                    }, ct));
            var results = await Task.WhenAll(subscriptions).ConfigureAwait(false);
            return new MqttClientSubscribeResult(ushort.MaxValue,
                results.SelectMany(r => r.Items).ToList(),
                results.Select(r => r.ReasonString).Aggregate((a, b) => a + " " + b),
                results.SelectMany(r => r.UserProperties).ToList());
        }

        /// <inheritdoc/>
        public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(
            MqttClientUnsubscribeOptions options, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            System.Diagnostics.Debug.Assert(_sessions.Length != 0);

            if (options.TopicFilters == null || options.TopicFilters.Count == 0)
            {
                throw new ArgumentException("No topic filters specified", nameof(options));
            }
            if (!_connection.IsCompleted)
            {
                await _connection.WaitAsync(ct).ConfigureAwait(false);
            }

            if (_sessions.Length == 1)
            {
                return await _sessions[0].UnsubscribeAsync(options, ct).ConfigureAwait(false);
            }

            if (options.TopicFilters.Count == 1)
            {
                return await GetSessionForTopic(options.TopicFilters[0])
                    .UnsubscribeAsync(options, ct).ConfigureAwait(false);
            }
            var subscriptions = options.TopicFilters
                .GroupBy(GetSessionIndex)
                .Select(group => _sessions[group.Key].UnsubscribeAsync(
                    new MqttClientUnsubscribeOptions
                    {
                        UserProperties = options.UserProperties,
                        TopicFilters = [.. group]
                    }, ct));
            var results = await Task.WhenAll(subscriptions).ConfigureAwait(false);
            return new MqttClientUnsubscribeResult(ushort.MaxValue,
                results.SelectMany(r => r.Items).ToList(),
                results.Select(r => r.ReasonString).Aggregate((a, b) => a + " " + b),
                results.SelectMany(r => r.UserProperties).ToList());
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct)
        {
            if (!TopicFilter.IsValid(topic))
            {
                throw new ArgumentException("Invalid topic filter", nameof(topic));
            }
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Register subscriptions
            IAsyncDisposable result;
            var subscribe = false;
            await _subscriptionsLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(topic, out var consumers))
                {
                    consumers = [consumer];
                    subscribe = true;
                    _subscriptions.Add(topic, consumers);
                }
                else
                {
                    consumers.Add(consumer);
                }
                result = new AsyncDisposable(() => UnsubscribeAsync(topic, consumer));
            }
            finally
            {
                _subscriptionsLock.Release();
            }

            //
            // Subscribe to the topic on a connected client, we let that happen on the
            // subscriber thread rather than using the managed client because we need
            // to always ensure that the subscriber is subscribed before the client
            // sends the first message here, specifically for the rpc case.
            //
            if (subscribe)
            {
                var filter = new MqttTopicFilter
                {
                    Topic = topic,
                    QualityOfServiceLevel = (MqttQualityOfServiceLevel)
                        (_options.Value.QoS ?? QoS.AtMostOnce)
                };
                try
                {
                    var tcs = new TaskCompletionSource();
                    _topics.Enqueue((tcs, filter));
                    _triggerSubscriber.Set();
                    await tcs.Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.SubscribeOnConnectFailed(ex);
                    throw;
                }
            }
            return result;
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, _publisher);
            return new MqttMessage(_options, _publisher);
        }

        /// <inheritdoc/>
        protected override ValueTask<IAsyncDisposable> SubscribeAsync(string topicFilter,
            CancellationToken ct)
        {
            return SubscribeAsync(topicFilter, IEventConsumer.Null, ct);
        }

        /// <inheritdoc/>
        public override async ValueTask PublishAsync(MqttApplicationMessage message,
            IEventSchema? schema, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();
            var client = await GetSessionAsync(message.Topic, ct).ConfigureAwait(false);
            await client.PublishAsync(message, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle connection
        /// </summary>
        /// <param name="session"></param>
        /// <param name="args"></param>
        private Task HandleSessionConnectedAsync(MqttSession session,
            MqttClientConnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _logger.SessionConnected(args.ConnectResult.ResultCode.ToString(), session.ClientId ?? "unknown");
            _triggerSubscriber.Set();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle close
        /// </summary>
        /// <param name="session"></param>
        /// <param name="args"></param>
        private Task HandleSessionClosedAsync(MqttSession session,
            MqttClientDisconnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            for (var i = 0; i < _sessions.Length && !_cts.IsCancellationRequested; i++)
            {
                if (_sessions[i] == session)
                {
                    _sessions[i] = CreateSession();
                    _connection = Task.WhenAll(_sessions
                        .Select(c => c.ConnectAsync(GetSessionClientOptions(), _cts.Token)));
                    _logger.SessionRecreated(session.ClientId ?? "unknown");
                    return Task.CompletedTask;
                }
            }
            _logger.SessionNotRecreated(session.ClientId ?? "unknown");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle message receival
        /// </summary>
        /// <param name="session"></param>
        /// <param name="args"></param>
        private async Task HandleMessageReceivedAsync(MqttSession session,
            MqttMessageReceivedEventArgs args)
        {
            if (args?.ApplicationMessage == null || _isDisposed)
            {
                return;
            }

            _logger.MessageReceivedTrace(session.ClientId ?? "unknown", args.ApplicationMessage.Topic);

            if (MessageReceived != null)
            {
                await MessageReceived.Invoke(args).ConfigureAwait(false);
                if (args.IsHandled)
                {
                    return;
                }
            }

            // Handle rpc outside of topic handling
            var processingFailed = args.ReasonCode != 0;
            if (await HandleRpcAsync(args.ApplicationMessage, processingFailed,
                (int)args.ReasonCode).ConfigureAwait(false))
            {
                return;
            }

            if (processingFailed)
            {
                _metrics.ProcessingFailed.Add(1);
                _logger.MessageProcessingFailed((int)args.ReasonCode);
                return;
            }

            // TODO: Add a wrapper interface over the list
            var properties = new Dictionary<string, string?>();
            if (args.ApplicationMessage.UserProperties != null)
            {
                foreach (var property in args.ApplicationMessage.UserProperties)
                {
                    properties.AddOrUpdate(property.Name, property.Value);
                }
            }

            await _subscriptionsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var topic = args.ApplicationMessage.Topic;
                foreach (var subscription in _subscriptions)
                {
                    if (TopicFilter.Matches(topic, subscription.Key))
                    {
                        foreach (var consumer in subscription.Value
                            .Where(s => s != IEventConsumer.Null))
                        {
                            await consumer.HandleAsync(topic,
                                args.ApplicationMessage.Payload,
                                args.ApplicationMessage.ContentType
                                    ?? "NoContentType_UseMqttv5",
                                properties, this).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                _subscriptionsLock.Release();
            }
        }

        /// <summary>
        /// Handle disconnected
        /// </summary>
        /// <param name="session"></param>
        /// <param name="args"></param>
        private Task HandleSessionDisconnectedAsync(MqttSession session,
            MqttClientDisconnectedEventArgs args)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _cts.Token.ThrowIfCancellationRequested();

            if (args.Exception != null)
            {
                _logger.SessionDisconnectedWithError(session.ClientId ?? "unknown",
                    args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason.ToString(), args.ReasonString ?? "unspecified",
                    args.Exception);
            }
            else
            {
                _logger.SessiontDisconnected(session.ClientId ?? "unknown",
                    args.ClientWasConnected ? "Connecting" : "Connected",
                    args.Reason.ToString(), args.ReasonString ?? "unspecified");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create all subscriptions as soon as possible
        /// </summary>
        /// <returns></returns>
        private async Task SubscribeAsync(CancellationToken ct)
        {
            await _connection.WaitAsync(ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                await _triggerSubscriber.WaitAsync(ct).ConfigureAwait(false);

                _triggerSubscriber.Reset();
                while (_topics.TryPeek(out var topic))
                {
                    // We try to subscribe here even if one client is not connected.
                    await _subscriptionsLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (_subscriptions.ContainsKey(topic.Item2.Topic))
                        {
                            var session = GetSessionForTopic(topic.Item2.Topic);

                            // Add as managed subscription
                            await session.SubscribeAsync(new MqttClientSubscribeOptions
                            {
                                TopicFilters = [topic.Item2]
                            }, ct).ConfigureAwait(false);
                        }
                        topic.Item1.TrySetResult();
                        _topics.TryDequeue(out _);
                    }
                    catch (MqttClientNotConnectedException nce)
                    {
                        _logger.SubscribeOnConnectFailed(nce);
                        // Rety
                    }
                    catch (Exception ex)
                    {
                        _logger.SubscribeFailed(ex);
                        topic.Item1.TrySetException(ex);
                        _topics.TryDequeue(out _);
                    }
                    finally
                    {
                        _subscriptionsLock.Release();
                    }
                }
            }
        }

        /// <summary>
        /// Remove subscription and unsubscribe if needed
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="consumer"></param>
        /// <exception cref="ResourceInvalidStateException"></exception>
        private async ValueTask UnsubscribeAsync(string topic, IEventConsumer consumer)
        {
            await _subscriptionsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_subscriptions.TryGetValue(topic, out var consumers))
                {
                    throw new ResourceInvalidStateException("Topic subscription not found");
                }
                consumers.Remove(consumer);
                if (consumers.Count == 0)
                {
                    await GetSessionForTopic(topic).UnsubscribeAsync(
                        new MqttClientUnsubscribeOptions
                        {
                            TopicFilters = [topic]
                        }, default).ConfigureAwait(false);
                    _subscriptions.Remove(topic);
                }
            }
            finally
            {
                _subscriptionsLock.Release();
            }
        }

        /// <summary>
        /// Get publisher client for topic waiting for connection
        /// </summary>
        private async ValueTask<MqttSession> GetSessionAsync(string topic,
            CancellationToken ct)
        {
            // Wait until connected
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_connection.IsCompleted)
            {
                await _connection.WaitAsync(ct).ConfigureAwait(false);
            }
            return GetSessionForTopic(topic);
        }

        /// <summary>
        /// Get session for topic
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private MqttSession GetSessionForTopic(string topic)
        {
            if (_sessions.Length == 1)
            {
                return _sessions[0];
            }
            System.Diagnostics.Debug.Assert(_sessions.Length > 0);

            // Use a string hash for the bucket for now. Could be better...
            return _sessions[GetSessionIndex(topic)];
        }

        private long GetSessionIndex(string topic)
        {
            return ((uint)topic.GetHashCode(StringComparison.Ordinal)) % _sessions.Length;
        }

        /// <summary>
        /// Create session options
        /// </summary>
        /// <returns></returns>
        private MqttClientOptions GetSessionClientOptions()
        {
            var tlsOptions = GetTlsOptions();
            var clientId = MqttSession.GetUniqueClientId(_options.Value.ClientId);
            var options = new MqttClientOptions
            {
                ProtocolVersion = ProtocolVersion,
                ClientId = clientId,

                SessionExpiryInterval = (uint?)_options.Value.SessionExpiry?.TotalSeconds ?? 3600,
                CleanSession = _options.Value.CleanStart ?? true,
                Credentials = _options.Value.UserName != null ? new MqttClientCredentials(
                    _options.Value.UserName, _options.Value.Password == null ?
                        (_options.Value.PasswordFile == null ? null :
                            File.ReadAllBytes(_options.Value.PasswordFile)) :
                        Encoding.UTF8.GetBytes(_options.Value.Password)) : null,
                ChannelOptions = _options.Value.WebSocketPath != null ?
                    new MqttClientWebSocketOptions
                    {
                        TlsOptions = tlsOptions,
                        Uri = new UriBuilder
                        {
                            Path = _options.Value.WebSocketPath,
                            Host = _options.Value.HostName ?? "localhost",
                            Scheme = _options.Value.UseTls == true ? "https" : "http",
                            Port = _options.Value.Port ?? 0
                        }.ToString()
                    } :
                    new MqttClientTcpOptions
                    {
                        RemoteEndpoint = new DnsEndPoint(
                            _options.Value.HostName ?? "localhost", _options.Value.Port ?? 1883),
                        TlsOptions = tlsOptions
                    },

                AuthenticationMethod = _options.Value.SatAuthFile == null ? null :
                    "K8S-SAT",
                AuthenticationData = _options.Value.SatAuthFile == null ? null :
                    File.ReadAllBytes(_options.Value.SatAuthFile),

                MaximumPacketSize = _options.Value.Protocol == MqttVersion.v5 ?
                    268435455u : 0u,
                KeepAlivePeriod = _options.Value.KeepAlivePeriod ?? TimeSpan.FromSeconds(15),
            };
            if (_options.Value.ReceiveMaximum != null)
            {
                options.ReceiveMaximum = _options.Value.ReceiveMaximum.Value;
            }
            _options.Value.ConfigureMqttClient?.Invoke(options);
            return options;

            MqttClientTlsOptions GetTlsOptions()
            {
                var certs = new List<X509Certificate2>();
                if (_options.Value.ClientCertificate != null)
                {
                    var cert = new X509Certificate2(_options.Value.ClientCertificate);
                    if (!cert.HasPrivateKey)
                    {
                        throw new SecurityException(
                            "Provided certificate is missing the private key information.");
                    }
                    certs.Add(cert);
                }
                else if (!string.IsNullOrEmpty(_options.Value.ClientCertificateFile) &&
                    !string.IsNullOrEmpty(_options.Value.ClientPrivateKeyFile))
                {
                    var cert = Load(_options.Value.ClientCertificateFile, _options.Value.ClientPrivateKeyFile,
                        _options.Value.PrivateKeyPasswordFile);
                    if (!cert.HasPrivateKey)
                    {
                        throw new SecurityException(
                            "Provided certificate is missing the private key information.");
                    }
                    certs.Add(cert);

                    static X509Certificate2 Load(string certFile, string keyFile,
                        string? keyFilePassword)
                    {
                        using var cert = string.IsNullOrEmpty(keyFilePassword) ?
                            X509Certificate2.CreateFromPemFile(certFile, keyFile) :
                            X509Certificate2.CreateFromEncryptedPemFile(certFile,
                                keyFilePassword, keyFile);
                        if (cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
                        {
                            throw new ArgumentException(
                    $"Cert '{cert.Subject}' expired '{cert.GetExpirationDateString()}'");
                        }
                        // https://github.com/dotnet/runtime/issues/45680#issuecomment-739912495
                        return X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Pkcs12));
                    }
                }

                var tlsParams = new MqttClientTlsOptions
                {
                    ClientCertificatesProvider =
                        new DefaultMqttCertificatesProvider(certs),
                    AllowUntrustedCertificates =
                        _options.Value.AllowUntrustedCertificates ?? false,
                    UseTls = _options.Value.UseTls ?? true,
                };

                if (_options.Value.TrustChain != null)
                {
                    tlsParams.TrustChain = _options.Value.TrustChain;
                    tlsParams.RevocationMode =
                        _options.Value.RequireRevocationCheck == true ?
                        X509RevocationMode.Online :
                        X509RevocationMode.NoCheck;
                }
                else if (!string.IsNullOrEmpty(_options.Value.IssuerCertFile))
                {
                    var caCerts = new X509Certificate2Collection();
                    caCerts.ImportFromPemFile(_options.Value.IssuerCertFile);
                    tlsParams.TrustChain = caCerts;
                    tlsParams.RevocationMode =
                        _options.Value.RequireRevocationCheck == true ?
                        X509RevocationMode.Online :
                        X509RevocationMode.NoCheck;
                }

                return tlsParams;
            }
        }

        private MqttSession CreateSession()
        {
            var session = new MqttSession(_logger, _options.Value, _meterProvider);

            session.Connected +=
                args => HandleSessionConnectedAsync(session, args);
            session.Disconnected +=
                args => HandleSessionDisconnectedAsync(session, args);
            session.MessageReceived +=
                args => HandleMessageReceivedAsync(session, args);
            session.SessionLost +=
                args => HandleSessionClosedAsync(session, args);

            return session;
        }

        /// <summary>
        /// Schema registry publisher
        /// </summary>
        internal sealed class MqttSchemaPublisher : IMqttPublish
        {
            /// <summary>
            /// Create schema registry implementation
            /// </summary>
            /// <param name="options"></param>
            /// <param name="publish"></param>
            /// <param name="registry"></param>
            public MqttSchemaPublisher(IOptions<MqttOptions> options, IMqttPublish publish,
                ISchemaRegistry? registry)
            {
                _version = options.Value.Protocol;
                _configure = options.Value.ConfigureSchemaMessage;
                _publish = publish;
                _registry = registry;
            }

            /// <summary>
            /// Publish
            /// </summary>
            /// <param name="message"></param>
            /// <param name="schema"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public async ValueTask PublishAsync(MqttApplicationMessage message,
                IEventSchema? schema, CancellationToken ct)
            {
                if (schema != null)
                {
                    string? id;
                    if (_registry == null)
                    {
                        //
                        // TODO: Hardcode a subpath and retain for now.
                        //
                        var builder = new MqttApplicationMessageBuilder()
                            .WithPayload(schema.Schema)
                            .WithTopic(message.Topic + "/schema")
                            .WithRetainFlag(true)
                            .WithContentType(schema.Type)
                            ;
                        var schemaMessage = builder.Build();
                        _configure?.Invoke(schemaMessage);
                        await _publish.PublishAsync(schemaMessage, null,
                            ct).ConfigureAwait(false);
                        id = schema.Id;
                    }
                    else
                    {
                        id = await _registry.RegisterAsync(schema, ct).ConfigureAwait(false);
                    }
                    if (id != null && _version != MqttVersion.v311)
                    {
                        // Add the schema id as cloud event property
                        message.UserProperties.Add(
                            new MqttUserProperty("dataschema", id));
                    }
                }
                await _publish.PublishAsync(message, schema, ct).ConfigureAwait(false);
            }

            private readonly MqttVersion _version;
            private readonly Action<MqttApplicationMessage>? _configure;
            private readonly IMqttPublish _publish;
            private readonly ISchemaRegistry? _registry;
        }

        private sealed record class Metrics : IDisposable
        {
            public Counter<long> ProcessingFailed { get; }

            /// <summary>
            /// Create metrics
            /// </summary>
            /// <param name="meter"></param>
            public Metrics(Meter meter)
            {
                _meter = meter;

                ProcessingFailed = meter.CreateCounter<long>("mqtt_client_processing_failed",
                    description: "The number of times a message received failed to be processed.");
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _meter.Dispose();
            }

            internal readonly Meter _meter;
        }

        private bool _isDisposed;
        private Task _connection;
        private readonly IOptions<MqttOptions> _options;
        private readonly ILogger _logger;
        private readonly Metrics _metrics;
        private readonly IMeterProvider _meterProvider;
        private readonly MqttSession[] _sessions;
        private readonly Task _subscriber;
        private readonly IMqttPublish _publisher;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _subscriptionsLock = new(1, 1);
        private readonly AsyncManualResetEvent _triggerSubscriber = new();
        private readonly ConcurrentQueue<(TaskCompletionSource, MqttTopicFilter)> _topics = new();
        private readonly Dictionary<string, List<IEventConsumer>> _subscriptions = [];
    }

    /// <summary>
    /// Source generated logging for MqttClient
    /// </summary>
    internal static partial class MqttClientLogging
    {
        private const int EventClass = 0;

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Closing mqtt client {ClientId} ...")]
        public static partial void ClientClosing(this ILogger logger, string clientId);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Error,
            Message = "Mqtt client Failed to stop rpc server.")]
        public static partial void RpcServerStopFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Error,
            Message = "Mqtt client Failed to stop subscriber.")]
        public static partial void SubscriberStopFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 4, Level = LogLevel.Error,
            Message = "Mqtt client {ClientId} Failed to stop mqtt session {SessionId}.")]
        public static partial void SessionCloseFailed(this ILogger logger, Exception ex, string clientId, string sessionId);

        [LoggerMessage(EventId = EventClass + 5, Level = LogLevel.Debug,
            Message = "Mqtt client Failed to subscribe on connect. Retrying...")]
        public static partial void SubscribeOnConnectFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 6, Level = LogLevel.Error,
            Message = "Mqtt client Failed to subscribe.")]
        public static partial void SubscribeFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = EventClass + 7, Level = LogLevel.Information,
            Message = "Mqtt session connected with {Result} as {SessionId}.")]
        public static partial void SessionConnected(this ILogger logger, string result, string sessionId);

        [LoggerMessage(EventId = EventClass + 8, Level = LogLevel.Trace,
            Message = "Mqtt session {SessionId} received message on {Topic}")]
        public static partial void MessageReceivedTrace(this ILogger logger, string sessionId, string topic);

        [LoggerMessage(EventId = EventClass + 9, Level = LogLevel.Warning,
            Message = "Mqtt session failed to process MQTT message: {ReasonCode}")]
        public static partial void MessageProcessingFailed(this ILogger logger, int reasonCode);

        [LoggerMessage(EventId = EventClass + 10, Level = LogLevel.Error,
            Message = "Mqtt session {SessionId} disconnected while {State} due to {Reason} ({ReasonString})")]
        public static partial void SessionDisconnectedWithError(this ILogger logger, string sessionId,
            string state, string reason, string reasonString, Exception ex);

        [LoggerMessage(EventId = EventClass + 11, Level = LogLevel.Warning,
            Message = "Mqtt session {SessionId} disconnected while {State} due to {Reason} ({ReasonString})")]
        public static partial void SessiontDisconnected(this ILogger logger, string sessionId,
            string state, string reason, string reasonString);

        [LoggerMessage(EventId = EventClass + 12, Level = LogLevel.Information,
            Message = "Mqtt client successfully recreated lost mqtt session {SessionId} ...")]
        public static partial void SessionRecreated(this ILogger logger, string sessionId);

        [LoggerMessage(EventId = EventClass + 13, Level = LogLevel.Information,
            Message = "Mqtt client was not able to recreat lost mqtt session {SessionId} ...")]
        public static partial void SessionNotRecreated(this ILogger logger, string sessionId);

        [LoggerMessage(EventId = EventClass + 14, Level = LogLevel.Information,
            Message = "Mqtt client {ClientId} successfully closed mqtt session {SessionId} ...")]
        public static partial void SessionClosed(this ILogger logger, string clientId, string sessionId);

        [LoggerMessage(EventId = EventClass + 15, Level = LogLevel.Information,
            Message = "Closed mqtt client {ClientId} ...")]
        public static partial void ClientClosed(this ILogger logger, string clientId);
    }
}
