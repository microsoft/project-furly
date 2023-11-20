// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel;
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a http handler using events and methods as tunnel.
    /// This is for when you need the edge to call cloud endpoints
    /// and tunnel these calls through multiple hops, e.g. in nested
    /// networking scenarios.
    /// The handler takes the http request and packages it into events
    /// sending it to <see cref="HttpTunnelHybridServer"/>. The
    /// server unpacks the events calls the endpoint and returns the
    /// response using <see cref="IMethodClient"/>, which causes this
    /// handler to be invoked as registered method invoker.
    /// It is thus important that this handler is also registered in
    /// the scope of the <see cref="ChunkMethodInvoker"/> and not just
    /// a <see cref="IHttpClientFactory"/>.
    /// </summary>
    public sealed class HttpTunnelHybridClientHandler :
        HttpTunnelBaseEventClientHandler
    {
        /// <summary>
        /// Create handler factory
        /// </summary>
        /// <param name="server"></param>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="timeout"></param>
        /// <param name="mount"></param>
        public HttpTunnelHybridClientHandler(IRpcServer server, IEventClient client,
            IJsonSerializer serializer, ILogger<HttpTunnelHybridClientHandler> logger,
            TimeSpan? timeout = null, string? mount = null) :
            base(client, serializer)
        {
            _server = server ??
                throw new ArgumentNullException(nameof(server));
            _chunks = new ChunkMethodServer(serializer, logger,
                timeout ?? TimeSpan.FromSeconds(30), mount) {
                new ResponseHandler(this)
            };
        }

        /// <inheritdoc/>
        protected override async Task<object?> OnRequestBeginAsync(string requestId,
            CancellationToken ct)
        {
            return await _server.ConnectAsync(_chunks, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task OnRequestEndAsync(string requestId,
            object? context, CancellationToken ct)
        {
            if (context is IAsyncDisposable connection)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _chunks.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Invoked for the response
        /// </summary>
        internal sealed class ResponseHandler : IMethodInvoker
        {
            /// <inheritdoc/>
            public string MethodName => MethodNames.Response;

            /// <summary>
            /// Create handler
            /// </summary>
            /// <param name="outer"></param>
            public ResponseHandler(HttpTunnelHybridClientHandler outer)
            {
                _outer = outer;
            }

            /// <inheritdoc/>
            public ValueTask<ReadOnlyMemory<byte>> InvokeAsync(
                ReadOnlyMemory<byte> payload, string contentType,
                IRpcHandler context, CancellationToken ct)
            {
                // Handle response from device method
                var response = _outer.Serializer
                    .Deserialize<HttpTunnelResponseModel>(payload);
                if (response == null)
                {
                    throw new ArgumentException("Malformed payload");
                }
                _outer.OnResponseReceived(response);
                return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
            }
            private readonly HttpTunnelHybridClientHandler _outer;
        }

        private readonly IRpcServer _server;
        private readonly ChunkMethodServer _chunks;
    }
}
