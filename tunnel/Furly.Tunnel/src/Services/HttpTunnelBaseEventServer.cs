// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides server side handling of tunnel requests and returns
    /// responses through a way defined by its sub class. Used as the
    /// cloud side end of a client initiated http tunnel.
    /// </summary>
    public abstract class HttpTunnelBaseEventServer : IEventConsumer,
        IAwaitable<HttpTunnelBaseEventServer>, IDisposable
    {
        /// <summary>
        /// Serializer
        /// </summary>
        protected IJsonSerializer Serializer { get; }

        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="receiver"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="timeProvider"></param>
        protected HttpTunnelBaseEventServer(ITunnelServer server,
            IEventSubscriber receiver, IJsonSerializer serializer,
            ILogger logger, TimeProvider? timeProvider)
        {
            Serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            _server = server ??
                throw new ArgumentNullException(nameof(server));
            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ??
                TimeProvider.System;

            // Subscribe for tunnel requests
            _subscription = receiver.SubscribeAsync(
                GetTopicString(HttpTunnelRequestModel.SchemaName, "+"), this).AsTask();
            _timer = new Timer(_ => OnTimer(), null,
                kTimeoutCheckInterval, kTimeoutCheckInterval);
        }

        /// <inheritdoc/>
        public IAwaiter<HttpTunnelBaseEventServer> GetAwaiter()
        {
            return _subscription.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async Task HandleAsync(string topic, ReadOnlySequence<byte> data,
            string contentType, IReadOnlyDictionary<string, string?> properties,
            IEventClient? responder, CancellationToken ct)
        {
            // Get message id and correlation id from content type
            var typeParsed = contentType.Split("_", StringSplitOptions.RemoveEmptyEntries);
            if (typeParsed.Length != 2 ||
                !int.TryParse(typeParsed[1], out var messageId))
            {
                _logger.LogError("Bad content type {ContentType} in tunnel event" +
                    " to {Topic}.", contentType, topic);
                return;
            }
            var requestId = typeParsed[0];

            HttpRequestProcessor? processor;
            if (messageId == 0)
            {
                try
                {
                    var chunk0 = Serializer.DeserializeRequest0(data.ToArray(),
                        out var request, out var chunks);
                    processor = new HttpRequestProcessor(this,
                        requestId, request, chunks, chunk0, null);
                    if (chunks != 0)
                    { // More to follow?
                        if (!_requests.TryAdd(requestId, processor))
                        {
                            throw new InvalidOperationException(
                                $"Adding request {requestId} failed.");
                        }
                        // Need more
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse tunnel request from {Topic} " +
                        "with id {RequestId} - giving up.", topic, requestId);
                    return;
                }
                // Complete request
            }
            else if (_requests.TryGetValue(requestId, out processor))
            {
                if (!processor.AddChunk(messageId, data.ToArray()))
                {
                    // Need more
                    return;
                }
                // Complete request
                _requests.TryRemove(requestId, out _);
            }
            else
            {
                // Timed out or expired
                _logger.LogDebug(
                    "Request from {Topic} with id {RequestId} timed out - give up.",
                    topic, requestId);
                return;
            }

            if (responder == null)
            {
                _logger.LogCritical("Cannot respond without responder!");
                return;
            }

            // Complete request
            try
            {
                await processor.CompleteAsync(responder, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete request from {Topic} " +
                    "with id {RequestId} - giving up.", topic, requestId);
            }
        }

        /// <summary>
        /// Dispose object
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _timer.Dispose();
                    _requests.Clear();
                    _subscription.Result.DisposeAsync().AsTask().Wait();
                }
                _disposedValue = true;
            }
        }

        /// <summary>
        /// Manage requests
        /// </summary>
        private void OnTimer()
        {
            foreach (var item in _requests.Values)
            {
                if (item.IsTimedOut)
                {
                    _requests.TryRemove(item.RequestId, out _);
                }
            }
        }

        /// <summary>
        /// Send the response
        /// </summary>
        /// <param name="responder"></param>
        /// <param name="response"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract Task RespondAsync(IEventClient responder,
            HttpTunnelResponseModel response, CancellationToken ct);

        /// <summary>
        /// Processes request chunks
        /// </summary>
        private class HttpRequestProcessor
        {
            /// <summary>
            /// Request handle
            /// </summary>
            public string RequestId { get; }

            /// <summary>
            /// Whether the request timed out
            /// </summary>
            public bool IsTimedOut
                => _outer._timeProvider.GetElapsedTime(_lastActivity) > _timeout;

            /// <summary>
            /// Create chunk
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="requestId"></param>
            /// <param name="request"></param>
            /// <param name="chunks"></param>
            /// <param name="chunk0"></param>
            /// <param name="timeout"></param>
            public HttpRequestProcessor(HttpTunnelBaseEventServer outer,
                string requestId, HttpTunnelRequestModel request,
                int chunks, byte[] chunk0, TimeSpan? timeout)
            {
                RequestId = requestId;
                _outer = outer;
                _timeout = timeout ?? TimeSpan.FromSeconds(20);
                _lastActivity = _outer._timeProvider.GetTimestamp();
                _request = request;
                _chunks = chunks + 1;
                _payload = new byte[_chunks][];
                _payload[0] = chunk0;
            }

            /// <summary>
            /// Perform the request
            /// </summary>
            /// <returns></returns>
            internal async Task CompleteAsync(IEventClient responder, CancellationToken ct)
            {
                try
                {
                    _request.Body = _payload.Unpack();
                    var response = await _outer._server.ProcessAsync(_request, ct).ConfigureAwait(false);

                    // Forward response back to source
                    await _outer.RespondAsync(responder, response, ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Forward failure back to source
                    await _outer.RespondAsync(responder, new HttpTunnelResponseModel
                    {
                        RequestId = RequestId,
                        Status = (int)HttpStatusCode.InternalServerError,
                        Payload = _outer.Serializer.SerializeToMemory(ex.Message).ToArray()
                    }, ct: ct).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Add payload
            /// </summary>
            /// <param name="id"></param>
            /// <param name="payload"></param>
            /// <returns></returns>
            internal bool AddChunk(int id, byte[] payload)
            {
                if (id < 0 || id >= _payload.Length || _payload[id] != null)
                {
                    return false;
                }
                _payload[id] = payload;
                _lastActivity = _outer._timeProvider.GetTimestamp();
                return !_payload.Any(p => p == null);
            }

            private readonly HttpTunnelBaseEventServer _outer;
            private readonly TimeSpan _timeout;
            private readonly HttpTunnelRequestModel _request;
            private readonly int _chunks;
            private readonly byte[][] _payload;
            private long _lastActivity;
        }

        /// <summary>
        /// Get topic for request
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="requestId"></param>
        /// <returns></returns>
        internal static string GetTopicString(string schema, string requestId)
        {
            var topicstr = new StringBuilder();
            topicstr.Append(schema);
            topicstr.Append('/');
            topicstr.Append(requestId);
            return topicstr.ToString();
        }

        private const int kTimeoutCheckInterval = 10000;
        private readonly ConcurrentDictionary<string, HttpRequestProcessor> _requests = new();
        private readonly Task<IAsyncDisposable> _subscription;
        private readonly Timer _timer;
        private readonly ITunnelServer _server;
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private bool _disposedValue;
    }
}
