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
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Linq;
    using System.Net;
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
    /// consumer unpacks the events calls the endpoint and returns the
    /// response using method client, which causes this handler to
    /// be invoked as method invoker.
    /// It is thus important that this handler is also registered in
    /// the scope of the <see cref="ChunkMethodInvoker"/> and not just
    /// a <see cref="IHttpClientFactory"/>.
    /// </summary>
    public abstract class HttpTunnelBaseEventClientHandler : HttpClientHandler
    {
        /// <inheritdoc/>
        public override bool SupportsAutomaticDecompression => true;

        /// <inheritdoc/>
        public override bool SupportsProxy => false;

        /// <inheritdoc/>
        public override bool SupportsRedirectConfiguration => false;

        /// <summary>
        /// Event client
        /// </summary>
        protected IEventClient Client { get; }

        /// <summary>
        /// Serializer
        /// </summary>
        protected IJsonSerializer Serializer { get; }

        /// <summary>
        /// Create handler factory
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        protected HttpTunnelBaseEventClientHandler(IEventClient client,
            IJsonSerializer serializer)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _outstanding = new ConcurrentDictionary<string, RequestTask>();
        }

        /// <summary>
        /// Handle response
        /// </summary>
        /// <param name="response"></param>
        /// <exception cref="ArgumentException"></exception>
        protected void OnResponseReceived(HttpTunnelResponseModel response)
        {
            if (_outstanding.TryRemove(response.RequestId, out var request))
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var httpResponse = new HttpResponseMessage((HttpStatusCode)response.Status)
                {
                    Content = response.Payload == null ? null :
                        new ByteArrayContent(response.Payload)
                };
#pragma warning restore CA2000 // Dispose objects before losing scope
                if (response.Headers != null)
                {
                    foreach (var header in response.Headers)
                    {
                        httpResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                request.Completion.TrySetResult(httpResponse);
                request.Dispose();
            }
        }

        /// <summary>
        /// Called before sending
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract Task<object?> OnRequestBeginAsync(string requestId,
            CancellationToken ct);

        /// <summary>
        /// Called before closing
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="context"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract Task OnRequestEndAsync(string requestId,
            object? context, CancellationToken ct);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // TODO: Investigate to remove all outstanding requests on the handler
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();

            // Create tunnel request
            var tunnelRequest = new HttpTunnelRequestModel
            {
                RequestId = requestId,
                Uri = request.RequestUri?.ToString()
                    ?? throw new ArgumentException("Uri missing"),
                RequestHeaders = request.Headers?
                    .ToDictionary(h => h.Key, h => h.Value.ToList()),
                Method = request.Method.ToString()
            };

            // Get content
            if (request.Content != null)
            {
                //payload = payload.Zip();

                tunnelRequest.Body = await request.Content.ReadAsByteArrayAsync(
                    cancellationToken).ConfigureAwait(false);
                tunnelRequest.ContentHeaders = request.Content.Headers?
                    .ToDictionary(h => h.Key, h => h.Value.ToList());
            }

            // Serialize
            var buffers = Serializer.SerializeRequest(tunnelRequest,
                Client.MaxEventPayloadSizeInBytes);

            var requestTask = new RequestTask(kDefaultTimeout, cancellationToken);
            if (!_outstanding.TryAdd(requestId, requestTask))
            {
                throw new InvalidOperationException("Could not add completion.");
            }

            var context = await OnRequestBeginAsync(requestId,
                cancellationToken).ConfigureAwait(false);
            try
            {
                // Send events
                for (var messageId = 0; messageId < buffers.Count; messageId++)
                {
                    await Client.SendEventAsync(HttpTunnelBaseEventServer.GetTopicString(
                        HttpTunnelRequestModel.SchemaName, requestId), buffers[messageId],
                        requestId + "_" + messageId.ToString(CultureInfo.InvariantCulture),
                        ct: cancellationToken).ConfigureAwait(false);
                }

                // Wait for completion
                try
                {
                    return await requestTask.Completion.Task.ConfigureAwait(false);
                }
                catch
                {
                    // If thrown remove and dispose first
                    if (_outstanding.TryRemove(requestId, out requestTask))
                    {
                        requestTask.Dispose();
                    }
                    throw;
                }
            }
            finally
            {
                await OnRequestEndAsync(requestId, context,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Request tasks
        /// </summary>
        private class RequestTask : IDisposable
        {
            /// <summary>
            /// Outstanding task
            /// </summary>
            public TaskCompletionSource<HttpResponseMessage> Completion { get; }
                = new TaskCompletionSource<HttpResponseMessage>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Create task
            /// </summary>
            /// <param name="timeout"></param>
            /// <param name="ct"></param>
            public RequestTask(TimeSpan timeout, CancellationToken ct)
            {
                _timeout = new CancellationTokenSource(timeout);
                ct.Register(() => _timeout.Cancel());
                // Register timeout handler
                _timeout.Token.Register(() =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        Completion.TrySetCanceled();
                    }
                    else
                    {
                        Completion.TrySetException(
                            new TimeoutException("Request timed out"));
                    }
                });
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _timeout.Dispose();
            }

            private readonly CancellationTokenSource _timeout;
        }

        private static readonly TimeSpan kDefaultTimeout = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, RequestTask> _outstanding;
    }
}
