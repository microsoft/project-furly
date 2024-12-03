// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Services
{
    using Furly.Tunnel.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Primitives;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles http requests through event client and passes them
    /// to the application server instance.  The tunnel can handle
    /// straight requests to a path identified by the method string
    /// or the tunnel model which provides a pure http request
    /// response pattern that includes content and request headers.
    /// </summary>
    public sealed class HttpTunnelRequestDelegate : ITunnelServer
    {
        /// <summary>
        /// Create tunnel
        /// </summary>
        /// <param name="services"></param>
        /// <param name="request"></param>
        public HttpTunnelRequestDelegate(IServiceProvider services, RequestDelegate request)
        {
            _delegate = request;
            _services = services;
        }

        /// <inheritdoc/>
        public async Task<HttpTunnelResponseModel> ProcessAsync(
            HttpTunnelRequestModel request, CancellationToken ct)
        {
            var uri = new Uri(request.Uri, UriKind.RelativeOrAbsolute);
            var httpRequest = new HttpTunnelRequest
            {
                Protocol = "TUNNEL",
                Method = request.Method ?? "GET",
                Payload = request.Body ?? [],
                RawTarget = request.Uri,
                Scheme = uri.IsAbsoluteUri ? uri.Scheme : string.Empty,
                Path = uri.IsAbsoluteUri ? uri.AbsolutePath : request.Uri,
                QueryString = uri.IsAbsoluteUri ? uri.Query : string.Empty,
                TraceIdentifier = request.RequestId,
            };

            if (request.ContentHeaders != null)
            {
                foreach (var item in request.ContentHeaders)
                {
                    httpRequest.Headers.TryAdd(item.Key,
                        new StringValues([.. item.Value]));
                }
            }
            if (request.RequestHeaders != null)
            {
                foreach (var item in request.RequestHeaders)
                {
                    httpRequest.Headers.TryAdd(item.Key,
                        new StringValues([.. item.Value]));
                }
            }

            // Create context
            var factory = _services.GetRequiredService<IHttpContextFactory>();
            var buffer = new MemoryStream();
            await using (buffer.ConfigureAwait(false))
            {
                var response = new HttpTunnelResponse(buffer);
                var features = new FeatureCollection();
                features.Set<IHttpRequestFeature>(httpRequest);
                features.Set<IHttpRequestIdentifierFeature>(httpRequest);
                features.Set<IHttpResponseFeature>(response);
                features.Set<IHttpResponseBodyFeature>(response);
                features.Set<IHttpBodyControlFeature>(response);
                var context = factory.Create(features);

                // Handle
                await _delegate(context).ConfigureAwait(false);

                // Serialize http back
                return new HttpTunnelResponseModel
                {
                    Payload = response.Payload,
                    RequestId = httpRequest.TraceIdentifier,
                    Status = response.StatusCode,
                    Reason = response.ReasonPhrase,
                    Headers = response.Headers?
                        .ToDictionary(k => k.Key, v => v.Value
                            .Select(v => v ?? string.Empty)
                            .ToList()),
                };
            }
        }

        /// <summary>
        /// Request
        /// </summary>
        private class HttpTunnelRequest : IHttpRequestFeature,
            IHttpRequestIdentifierFeature
        {
            /// <inheritdoc/>
            public Stream Body { get; set; } = new MemoryStream();

            /// <summary>
            /// Payload
            /// </summary>
            internal byte[]? Payload
            {
                get => (Body as MemoryStream)?.ToArray();
                set
                {
                    if (value != null)
                    {
                        Body = new MemoryStream(value);
                    }
                }
            }

            /// <inheritdoc/>
            public IHeaderDictionary Headers { get; set; }
                = new HeaderDictionary();

            /// <inheritdoc/>
            public string Method { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string Path { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string PathBase { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string Protocol { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string QueryString { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string RawTarget { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string Scheme { get; set; } = string.Empty;
            /// <inheritdoc/>
            public string TraceIdentifier { get; set; } = string.Empty;
        }

        /// <summary>
        /// Response
        /// </summary>
        private class HttpTunnelResponse : StreamResponseBodyFeature,
            IHttpResponseFeature, IHttpBodyControlFeature
        {
            /// <inheritdoc/>
            public HttpTunnelResponse(Stream stream)
                : base(stream)
            {
            }

            /// <inheritdoc/>
            public Stream Body
            {
                get => Stream;
                set => throw new NotSupportedException();
            }

            internal byte[]? Payload =>
                (Stream as MemoryStream)?.ToArray();

            /// <inheritdoc/>
            public bool HasStarted =>
                Body.Position != 0;
            /// <inheritdoc/>
            public IHeaderDictionary Headers { get; set; } =
                new HeaderDictionary();
            /// <inheritdoc/>
            public string? ReasonPhrase { get; set; } = string.Empty;
            /// <inheritdoc/>
            public int StatusCode { get; set; } =
                (int)HttpStatusCode.OK;

            /// <inheritdoc/>
            public void OnCompleted(Func<object, Task> callback,
                object state)
            {
            }

            /// <inheritdoc/>
            public void OnStarting(Func<object, Task> callback,
                object state)
            {
            }

            /// <inheritdoc/>
            public bool AllowSynchronousIO { get; set; }
        }

        private readonly IServiceProvider _services;
        private readonly RequestDelegate _delegate;
    }
}
