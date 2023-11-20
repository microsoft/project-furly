// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel;
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This handler allows cloud to module HTTP tunnelling.
    /// It provides a http handler using method client as tunnel.
    /// The request is chunked through method calls to the edge
    /// side <see cref="ChunkMethodInvoker"/>, unpacked and the
    /// actual call is performed. Note that if the uri is relative
    /// the call is handled by the host, if the Uri provided is
    /// absolute it is unpacked into a local and vanilla HTTP
    /// call performed through an HttpClient.
    /// </summary>
    public sealed class HttpTunnelMethodClientHandler : HttpClientHandler
    {
        /// <inheritdoc/>
        public override bool SupportsAutomaticDecompression => true;

        /// <inheritdoc/>
        public override bool SupportsProxy => false;

        /// <inheritdoc/>
        public override bool SupportsRedirectConfiguration => false;

        /// <summary>
        /// Create handler
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="loggerFactory"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpTunnelMethodClientHandler(IRpcClient client,
            IJsonSerializer serializer, ILoggerFactory loggerFactory)
        {
            _serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            _client = new ChunkMethodClient(client, serializer,
                loggerFactory.CreateLogger<ChunkMethodClient>());
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();
            if (request.RequestUri == null)
            {
                throw new ArgumentException("Missing uri in request.");
            }
            // Create tunnel request
            var trequest = new HttpTunnelRequestModel
            {
                RequestId = requestId,
                Uri = request.RequestUri.ToString(),
                RequestHeaders = request.Headers?
                    .ToDictionary(h => h.Key, h => h.Value.ToList()),
                Method = request.Method.ToString()
            };

            // Get content
            byte[]? payload = null;
            if (request.Content != null)
            {
                payload = await request.Content.ReadAsByteArrayAsync(
                    cancellationToken).ConfigureAwait(false);
                trequest.Body = payload;
                trequest.ContentHeaders = request.Content.Headers?
                    .ToDictionary(h => h.Key, h => h.Value.ToList());
            }

            // Get target of the invocation - if not provided will use the default configured
            if (!request.Options.TryGetValue(new HttpRequestOptionsKey<string>(kTargetOption),
                out var target))
            {
                if (Properties.TryGetValue(kTargetOption, out var o))
                {
                    target = o?.ToString();
                }
                if (string.IsNullOrEmpty(target))
                {
                    // Set default target to be the host name
                    target = (Proxy?.GetProxy(request.RequestUri)?.Host) ??
                        request.RequestUri.Host;
                }
            }

            var input = _serializer.SerializeToMemory(trequest).ToArray();
            var output = await _client.CallMethodAsync(target,
                "$tunnel", input, HttpTunnelRequestModel.SchemaName,
                kDefaultTimeout, cancellationToken).ConfigureAwait(false);
            var tResponse = _serializer
                .Deserialize<HttpTunnelResponseModel>(output);
            var response = new HttpResponseMessage(
                ((HttpStatusCode?)tResponse?.Status) ?? HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = tResponse?.Reason ?? "Bad response was returned",
                RequestMessage = request,
                Content = tResponse?.Payload == null ? null :
                    new ByteArrayContent(tResponse.Payload)
            };
            if (tResponse?.Headers != null)
            {
                foreach (var header in tResponse.Headers)
                {
                    response.Headers.TryAddWithoutValidation(
                        header.Key, header.Value);
                }
            }
            return response;
        }

        private const string kTargetOption = "target";
        private static readonly TimeSpan kDefaultTimeout = TimeSpan.FromMinutes(5);
        private readonly ChunkMethodClient _client;
        private readonly IJsonSerializer _serializer;
    }
}
