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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a http handler using event messages as tunnel.
    /// This is for when you need the edge to call cloud endpoints
    /// and tunnel these calls through multiple hops, e.g. in nested
    /// networking scenarios and responses back to caller.
    /// The handler takes the http request and packages it into events
    /// sending it to <see cref="HttpTunnelHybridServer"/>. The
    /// consumer unpacks the events calls the endpoint and returns the
    /// response packaged again as events, which causes this handler
    /// to again be invoked for every message.
    /// </summary>
    public sealed class HttpTunnelEventClientHandler : HttpTunnelBaseEventClientHandler,
        IEventConsumer
    {
        /// <summary>
        /// Create handler factory
        /// </summary>
        /// <param name="client"></param>
        /// <param name="subscriber"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public HttpTunnelEventClientHandler(IEventClient client, IEventSubscriber subscriber,
            IJsonSerializer serializer, ILogger<HttpTunnelEventClientHandler> logger) :
            base(client, serializer)
        {
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        protected override async Task<object?> OnRequestBeginAsync(string requestId,
            CancellationToken ct)
        {
            // Subscribe to response events
            var subscription = await _subscriber.SubscribeAsync(
                HttpTunnelBaseEventServer.GetTopicString(HttpTunnelResponseModel.SchemaName,
                requestId), this, ct).ConfigureAwait(false);
            var processor = new HttpResponseProcessor(this, requestId, subscription);
            if (!_responses.TryAdd(requestId, processor))
            {
                await processor.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Failed to add request");
            }
            return processor;
        }

        /// <inheritdoc/>
        public Task HandleAsync(string topic, ReadOnlySequence<byte> data, string contentType,
            IReadOnlyDictionary<string, string?> properties, IEventClient? responder,
            CancellationToken ct = default)
        {
            // Get message id and correlation id from content type
            var typeParsed = contentType.Split("_", StringSplitOptions.RemoveEmptyEntries);
            if (typeParsed.Length != 2 ||
                !int.TryParse(typeParsed[1], out var messageId))
            {
                _logger.LogError("Bad content type {ContentType} in tunnel event" +
                    " from {RequestId}.", contentType, topic);
                return Task.CompletedTask;
            }
            var requestId = typeParsed[0];
            if (_responses.TryGetValue(requestId, out var processor))
            {
                processor.Process(messageId, data.ToArray());
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task OnRequestEndAsync(string requestId,
            object? context, CancellationToken ct)
        {
            if (_responses.TryRemove(requestId, out var processor))
            {
                System.Diagnostics.Debug.Assert(context == processor);
                await processor.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Response processor - managed by super class's request task
        /// lifecycle (based on the callbacks)
        /// </summary>
        private class HttpResponseProcessor : IAsyncDisposable
        {
            /// <summary>
            /// Create processor
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="requestId"></param>
            /// <param name="subscription"></param>
            public HttpResponseProcessor(HttpTunnelEventClientHandler outer,
                string requestId, IAsyncDisposable subscription)
            {
                _outer = outer;
                _requestId = requestId;
                _subscription = subscription;
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                return _subscription.DisposeAsync();
            }

            /// <summary>
            /// Handle data received on topic
            /// </summary>
            /// <param name="messageId"></param>
            /// <param name="data"></param>
            /// <returns></returns>
            internal void Process(int messageId, byte[] data)
            {
                if (messageId == 0)
                {
                    var chunk0 = _outer.Serializer.DeserializeResponse0(
                        data, out _response, out _numberOfChunks);
                    if (chunk0.Length > 0)
                    {
                        _numberOfChunks++; // Include chunk 0 in chunk count
                        if (!AddChunk(messageId, chunk0))
                        {
                            // Need more
                            return;
                        }
                    }
                    else if (_numberOfChunks > 0)
                    {
                        return; // More to follow in chunk 1..numberOfChunks
                    }
                }
                else if (!AddChunk(messageId, data))
                {
                    // Need more - still not enough
                    return;
                }
                // Complete request
                System.Diagnostics.Debug.Assert(_response != null);
                _response.Payload = _chunks.Values.ToArray().Unpack();
                _response.RequestId = _requestId;
                _outer.OnResponseReceived(_response);
            }

            /// <summary>
            /// Add payload
            /// </summary>
            /// <param name="id"></param>
            /// <param name="payload"></param>
            /// <returns></returns>
            internal bool AddChunk(int id, byte[] payload)
            {
                if (id < 0 || id >= kMaxNumberOfChunks)
                {
                    return false;
                }
                if (_numberOfChunks >= 0 && id >= _numberOfChunks)
                {
                    return false;
                }
                if (!_chunks.TryAdd(id, payload))
                {
                    return false; // Already exists
                }
                return _chunks.Count == _numberOfChunks;
            }

            private HttpTunnelResponseModel? _response;
            private int _numberOfChunks = -1;
            private readonly ConcurrentDictionary<int, byte[]> _chunks = new();
            private readonly IAsyncDisposable _subscription;
            private readonly string _requestId;
            private readonly HttpTunnelEventClientHandler _outer;
        }

        private const int kMaxNumberOfChunks = 1024;
        private readonly IEventSubscriber _subscriber;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, HttpResponseProcessor> _responses = new();
    }
}
