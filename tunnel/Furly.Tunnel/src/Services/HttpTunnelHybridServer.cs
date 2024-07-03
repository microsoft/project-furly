// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel;
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System;

    /// <summary>
    /// Provides server side handling of tunnel requests and returns
    /// responses through <see cref="IMethodClient"/> to the method
    /// server running at the edge. Used as the cloud side of
    /// a http tunnel.
    /// </summary>
    public sealed class HttpTunnelHybridServer : HttpTunnelBaseEventServer
    {
        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="subscriber"></param>
        /// <param name="responder"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="timeProvider"></param>
        public HttpTunnelHybridServer(ITunnelServer server, IEventSubscriber subscriber,
            IRpcClient responder, IJsonSerializer serializer, ILoggerFactory logger,
            TimeProvider? timeProvider = null) : base(
            server, subscriber, serializer, logger.CreateLogger<HttpTunnelHybridServer>(),
                timeProvider)
        {
            _responder = new ChunkMethodClient(responder, serializer,
                logger.CreateLogger<ChunkMethodClient>());
        }

        /// <inheritdoc/>
        protected override async Task RespondAsync(IEventClient responder,
            HttpTunnelResponseModel response, CancellationToken ct = default)
        {
            var json = Serializer.SerializeToString(response);
            await _responder.CallMethodAsync(responder.Identity,
                MethodNames.Response, Encoding.UTF8.GetBytes(json),
                ContentMimeType.Json, null, ct).ConfigureAwait(false);
        }

        private readonly ChunkMethodClient _responder;
    }
}
