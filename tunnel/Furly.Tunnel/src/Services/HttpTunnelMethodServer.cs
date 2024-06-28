// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles http requests through chunk server and passes them
    /// to the respective endpoint via http client factory.
    /// The tunnel can handle straight requests to a path identified
    /// by the method string or the tunnel model which provides a
    /// pure http request response pattern that includes content
    /// and request headers.
    /// </summary>
    public sealed class HttpTunnelMethodServer : IRpcHandler,
        IAwaitable<HttpTunnelMethodServer>, IDisposable, IAsyncDisposable
    {
        /// <inheritdoc/>
        public string MountPoint { get; }

        /// <summary>
        /// Create the server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="processor"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="timeout"></param>
        /// <param name="mount"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpTunnelMethodServer(IRpcServer server,
            ITunnelServer processor, IJsonSerializer serializer,
            ILogger<HttpTunnelMethodServer> logger,
            TimeSpan? timeout = null, string? mount = null)
        {
            MountPoint = mount ?? string.Empty;

            _serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            _processor = processor ??
                throw new ArgumentNullException(nameof(processor));

            // Start to listen on the connect
            _connection = server.ConnectAsync(this).AsTask();
            _chunks = new ChunkMethodInvoker(_serializer, logger,
                timeout ?? TimeSpan.FromSeconds(30));
        }

        /// <inheritdoc/>
        public IAwaiter<HttpTunnelMethodServer> GetAwaiter()
        {
            return _connection.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _chunks.Dispose();
            var connection = await _connection.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> InvokeAsync(string method,
            ReadOnlyMemory<byte> payload, string contentType, CancellationToken ct)
        {
            if (method == _chunks.MethodName)
            {
                // Pass to chunk server
                return await _chunks.InvokeAsync(payload, contentType, this,
                    ct).ConfigureAwait(false);
            }

            var isSimpleCall = contentType != HttpTunnelRequestModel.SchemaName;
            using var request = new HttpRequestMessage();
            if (isSimpleCall)
            {
                if (contentType != null && contentType != ContentMimeType.Json)
                {
                    throw new ArgumentException(
                        $"{contentType} must be null or {ContentMimeType.Json}",
                            nameof(contentType));
                }

                if (string.IsNullOrEmpty(method))
                {
                    method = "/";
                }
                else if (method[0] != '/')
                {
                    method = "/" + method;
                }

                var mediaType = new MediaTypeHeaderValue(ContentMimeType.Json)
                {
                    CharSet = "Utf-8"
                };

                var inbound = new HttpTunnelRequestModel
                {
                    RequestId = string.Empty,
                    ContentHeaders = new Dictionary<string, List<string>>()
                    {
                        ["Content-Type"] = new List<string> { mediaType.ToString() }
                    },
                    Method = "POST",
                    Uri = method,
                    Body = payload.ToArray()
                };
                var outbound = await _processor.ProcessAsync(inbound, ct).ConfigureAwait(false);
                if (outbound.Status != (int)HttpStatusCode.OK)
                {
                    MethodCallStatusException.TryThrow(outbound.Payload, _serializer,
                        outbound.Status);
                }
                return outbound.Payload ?? Array.Empty<byte>();
            }
            else
            {
                // Deserialize http tunnel payload
                var inbound = _serializer.Deserialize<HttpTunnelRequestModel>(payload);
                if (inbound == null)
                {
                    throw new ArgumentException("Bad payload");
                }
                var outbound = await _processor.ProcessAsync(inbound, ct).ConfigureAwait(false);
                return _serializer.SerializeToMemory(outbound).ToArray();
            }
        }

        private readonly ChunkMethodInvoker _chunks;
        private readonly IJsonSerializer _serializer;
        private readonly ITunnelServer _processor;
        private readonly Task<IAsyncDisposable> _connection;
    }
}
