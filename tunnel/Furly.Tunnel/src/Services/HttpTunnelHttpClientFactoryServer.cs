// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel.Models;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Http client as tunnel server
    /// </summary>
    public sealed class HttpTunnelHttpClientFactoryServer : ITunnelServer
    {
        /// <summary>
        /// Create processor
        /// </summary>
        /// <param name="http"></param>
        public HttpTunnelHttpClientFactoryServer(IHttpClientFactory http)
        {
            _http = http ??
                throw new ArgumentNullException(nameof(http));
        }

        /// <summary>
        /// Process request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<HttpTunnelResponseModel> ProcessAsync(HttpTunnelRequestModel request,
            CancellationToken ct)
        {
            var content = new ByteArrayContent(request.Body ?? []);
            // Add content headers
            if (request.ContentHeaders != null)
            {
                foreach (var header in request.ContentHeaders)
                {
                    content.Headers.TryAddWithoutValidation(
                        header.Key, header.Value);
                }
            }
            using var httpRequest = new HttpRequestMessage(
                new HttpMethod(request.Method.ToUpperInvariant()), request.Uri)
            {
                Content = content
            };

            // Add remaining headers
            if (request.RequestHeaders != null)
            {
                foreach (var header in request.RequestHeaders)
                {
                    httpRequest.Headers.TryAddWithoutValidation(
                        header.Key, header.Value);
                }
            }

            // Perform request
            using var httpClient = _http.CreateClient();
            var response = await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            if (response == null)
            {
                throw new InvalidOperationException("Failed to get response.");
            }
            return new HttpTunnelResponseModel
            {
                Headers = response.Headers?
                                    .ToDictionary(h => h.Key, h => h.Value.ToList()),
                RequestId = request.RequestId,
                Status = (int)response.StatusCode,
                Payload = await response.Content
                                    .ReadAsByteArrayAsync(ct).ConfigureAwait(false)
            };
        }
        private readonly IHttpClientFactory _http;
    }
}
