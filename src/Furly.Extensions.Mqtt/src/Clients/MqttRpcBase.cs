// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Rpc;
    using Furly.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MQTTnet;
    using MQTTnet.Packets;
    using MQTTnet.Protocol;
    using Nito.Disposables;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Furly.Extensions.Messaging;

    /// <summary>
    /// Mqtt rpc client base
    /// </summary>
    public abstract class MqttRpcBase : IRpcClient, IRpcServer
    {
        /// <inheritdoc/>
        public string Name => "Mqtt";

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes { get; protected set; }

        /// <inheritdoc/>
        public IEnumerable<IRpcHandler> Connected => _handlers.Values.Select(v => v.Item1);

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        protected MqttRpcBase(IOptions<MqttOptions> options, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            MaxMethodPayloadSizeInBytes =
                Math.Max(_options.Value.MaxPayloadSize ?? int.MaxValue, 268435455); // (256 MB)
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html#_Toc398718023
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> ConnectAsync(IRpcHandler server,
            CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isClosed, this);
            var id = Guid.NewGuid();

            var serverTopic = $"{server.MountPoint.TrimEnd('/')}/#";
            var subscription = await SubscribeAsync(serverTopic, ct).ConfigureAwait(false);

            if (!_handlers.TryAdd(id, (server, subscription)))
            {
                await subscription.DisposeAsync().ConfigureAwait(false);
                throw new ResourceExhaustionException("Failed to add handler");
            }

            return new AsyncDisposable(async () =>
            {
                if (_handlers.TryRemove(id, out var handler))
                {
                    await handler.Item2.DisposeAsync().ConfigureAwait(false);
                }
            });
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            var callTimeout = timeout ??
                _options.Value.DefaultMethodCallTimeout ?? TimeSpan.FromSeconds(30);
            var attempt = -1;
            for (; attempt < (_options.Value.MethodCallTimeoutRetries ?? 1); attempt++)
            {
                ObjectDisposedException.ThrowIf(_isClosed, this);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(callTimeout);
                try
                {
                    return await CallInternalAsync(target, method, payload, contentType,
                        cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogDebug("Retry call after timeout...");
                }
            }
            throw new MethodCallException(
                $"Timed out calling method {method} after {attempt + 1} attempts." +
                $"Broker {_options.Value.HostName}:{_options.Value.Port} possibly unreachable.");
        }

        /// <summary>
        /// Subscribe to topic
        /// </summary>
        /// <param name="topicFilter"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract ValueTask<IAsyncDisposable> SubscribeAsync(
            string topicFilter, CancellationToken ct);

        /// <summary>
        /// Publish reqeusts and responses
        /// </summary>
        /// <param name="message"></param>
        /// <param name="schema"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public abstract ValueTask PublishAsync(MqttApplicationMessage message,
            IEventSchema? schema, CancellationToken ct);

        /// <summary>
        /// Mark closed
        /// </summary>
        protected void Close()
        {
            _isClosed = true;
        }

        /// <summary>
        /// Handle rpc messages
        /// </summary>
        /// <param name="message"></param>
        /// <param name="processingFailed"></param>
        /// <param name="reasonCode"></param>
        /// <returns></returns>
        protected async Task<bool> HandleRpcAsync(MqttApplicationMessage message,
            bool processingFailed, int reasonCode)
        {
            var topic = message.Topic;
            if ((_pending.IsEmpty && _handlers.IsEmpty) || _isClosed)
            {
                return false;
            }

            // Get request id, method and topic root from message
            if (!ParseMessage(message, out var isRequest, out var requestId,
                out var method, out var topicRoot))
            {
                return false;
            }

            // Handle any pending requests
            if (!isRequest &&
                _pending.TryRemove(requestId, out var pending) && (!processingFailed ?
                pending.TrySetResult((topic, message)) : pending.TrySetException(
                    new MethodCallException(reasonCode.ToString(CultureInfo.InvariantCulture)))))
            {
                return true;
            }

            if (isRequest && !_handlers.IsEmpty && method != null)
            {
                var payload = ReadOnlyMemory<byte>.Empty;
                if (!processingFailed)
                {
                    (payload, reasonCode) = await InvokeAsync(method, message.PayloadSegment,
                            message.ContentType ?? ContentMimeType.Json,
                            default).ConfigureAwait(false);
                }
                var statusCode = reasonCode.ToString(CultureInfo.InvariantCulture);
                if (message.ResponseTopic != null)
                {
                    if (payload.IsEmpty)
                    {
                        // Work around mqttnet rpc client bugs
                        payload = kEmptyPayload;
                    }
                    await PublishAsync(message.ResponseTopic, null, payload,
                        properties: new[] { new MqttUserProperty(kStatusCodeKey, statusCode) },
                        correlationData: message.CorrelationData).ConfigureAwait(false);
                }
                else
                {
                    // Send reply
                    await PublishAsync(
                        $"{topicRoot}/{kResPath}/{statusCode}/{kRequestIdKey}{requestId}",
                        null, payload).ConfigureAwait(false);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Call method
        /// </summary>
        /// <param name="target"></param>
        /// <param name="method"></param>
        /// <param name="buffer"></param>
        /// <param name="contentType"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="MethodCallException"></exception>
        /// <exception cref="MethodCallStatusException"></exception>
        private async ValueTask<ReadOnlyMemory<byte>> CallInternalAsync(string target, string method,
            ReadOnlyMemory<byte> buffer, string contentType, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<(string, MqttApplicationMessage)>();
            ct.Register(() => tcs.TrySetCanceled());

            var requestId = Guid.NewGuid();
            IAsyncDisposable? subscription = null;
            try
            {
                var status = 0;
                MqttApplicationMessage message;
                string topic;
                if (_options.Value.Protocol != MqttVersion.v311)
                {
                    var responseTopic = $"{_options.Value.ClientId}/responses";
                    subscription = await SubscribeAsync(responseTopic + "/#", ct).ConfigureAwait(false);
                    // Add pending completion
                    _pending.TryAdd(requestId, tcs);

                    await PublishAsync($"{target}/{method}", responseTopic, buffer, contentType,
                        correlationData: requestId.ToByteArray(), ct: ct).ConfigureAwait(false);

                    ct.ThrowIfCancellationRequested();
                    (_, message) = await tcs.Task.ConfigureAwait(false);

                    status = int.Parse(message.UserProperties
                        .Find(p => p.Name == kStatusCodeKey)?.Value ?? "500",
                            CultureInfo.InvariantCulture);
                }
                else
                {
                    // Add pending completion
                    var responseTopic = $"{target}/{kResPath}/+/+";
                    subscription = await SubscribeAsync(responseTopic, ct).ConfigureAwait(false);
                    _pending.TryAdd(requestId, tcs);

                    // On 3.11 we pass the correlation id through the topic path
                    await PublishAsync($"{target}/{method}/{kRequestIdKey}{requestId}",
                        null, buffer, contentType, ct: ct).ConfigureAwait(false);

                    ct.ThrowIfCancellationRequested();
                    (topic, message) = await tcs.Task.ConfigureAwait(false);

                    var components = topic.Replace($"{target}/{kResPath}/", "",
                        StringComparison.Ordinal).Split('/');
                    status = int.Parse(components[^2], CultureInfo.InvariantCulture);
                    if (requestId.ToString() != components[^1][kRequestIdKey.Length..])
                    {
                        throw new MethodCallException("Did not get correct request id back.");
                    }
                }
                return status == 200 ? message.PayloadSegment :
                    throw new MethodCallStatusException(message.PayloadSegment, status);
            }
            finally
            {
                _pending.TryRemove(requestId, out _);
                if (subscription != null)
                {
                    await subscription.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Send event as MQTT message with configured properties for the client.
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="responseTopic"></param>
        /// <param name="payload"></param>
        /// <param name="contentType"></param>
        /// <param name="properties"></param>
        /// <param name="correlationData"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task PublishAsync(string topic, string? responseTopic,
            ReadOnlyMemory<byte> payload = default, string? contentType = null,
            IReadOnlyList<MqttUserProperty>? properties = null, byte[]? correlationData = null,
            CancellationToken ct = default)
        {
            var message = new MqttApplicationMessage
            {
                Topic = topic,
                ResponseTopic = responseTopic,
                PayloadSegment = payload.ToArray(),
                UserProperties = _options.Value.Protocol == MqttVersion.v311
                    ? null : properties?.ToList(),
                ContentType = _options.Value.Protocol == MqttVersion.v311
                    ? null : contentType,
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                CorrelationData = correlationData,
                Retain = false
            };
            await PublishAsync(message, null, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Get correlation id from message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="isRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="methodName"></param>
        /// <param name="topicRoot"></param>
        /// <returns></returns>
        private static bool ParseMessage(MqttApplicationMessage message,
            out bool isRequest, out Guid requestId,
            out string? methodName, out string? topicRoot)
        {
            var components = message.Topic.Split('/');
            var last = components[^1];
            if (message.CorrelationData != null || message.ResponseTopic != null)
            {
                //
                // Mqtt5 mode. The message is a request if it
                // contains the a response topic, and a response
                // otherwise (we map to pending request id then).
                //
                methodName = last;
                requestId = message.CorrelationData?.Length == 16 ?
                    new Guid(message.CorrelationData) : Guid.NewGuid();
                if (components.Length < 2)
                {
                    // Not us
                    isRequest = false;
                    topicRoot = default;
                    return false;
                }

                topicRoot = message.Topic[..(last.Length + 1)];
                isRequest = message.ResponseTopic != null;
                return true;
            }

            if (!last.StartsWith(kRequestIdKey, StringComparison.Ordinal) ||
                components.Length < 2)
            {
                methodName = default;
                requestId = default;
                isRequest = false;
                topicRoot = default;
                return false;
            }

            //
            // Mqtt3 mode. The topic must have the request query param
            // and must have 2 components at least (name or
            // res + statuscode and the request id.
            //
            if (components.Length >= 3 && components[^3] == kResPath)
            {
                // Response
                methodName = default;
                topicRoot = message.Topic.Split(kResPath)[0].TrimEnd('/');
                isRequest = false;
            }
            else
            {
                methodName = components[^2];
                topicRoot = message.Topic.Split(methodName)[0].TrimEnd('/');
                isRequest = true;
            }
            return Guid.TryParse(last.AsSpan(6), out requestId);
        }

        /// <summary>
        /// Handle method invocation
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payload"></param>
        /// <param name="contentType"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<(ReadOnlyMemory<byte>, int)> InvokeAsync(string method,
            ReadOnlyMemory<byte> payload, string contentType, CancellationToken ct)
        {
            foreach (var (server, _) in _handlers.Values)
            {
                try
                {
                    var result = await server.InvokeAsync(method,
                        payload.ToArray(), contentType, ct).ConfigureAwait(false);
                    if (result.Length > MaxMethodPayloadSizeInBytes)
                    {
                        _logger.LogError("Result (Payload too large => {Length}",
                            result.Length);
                        return (default, (int)HttpStatusCode.RequestEntityTooLarge);
                    }
                    return (result, 200);
                }
                catch (MethodCallStatusException mex)
                {
                    payload = mex.ResponsePayload;
                    return (payload.Length > MaxMethodPayloadSizeInBytes ? null :
                        payload, mex.Result);
                }
                catch (NotSupportedException)
                {
                    // Continue
                }
                catch (Exception)
                {
                    return (default, (int)HttpStatusCode.MethodNotAllowed);
                }
            }
            return (default, 500);
        }

        private static readonly ReadOnlyMemory<byte> kEmptyPayload = new byte[] { 0 };
        private const string kResPath = "res";
        private const string kRequestIdKey = "?$rid=";
        private const string kStatusCodeKey = "StatusCode";
        private readonly IOptions<MqttOptions> _options;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid,
            (IRpcHandler, IAsyncDisposable)> _handlers = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<
            (string, MqttApplicationMessage)>> _pending = new();
        private bool _isClosed;
    }
}
