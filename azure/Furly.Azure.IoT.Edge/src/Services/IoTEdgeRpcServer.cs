// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure.IoT.Edge;
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Furly.Extensions.Serializers;

    /// <summary>
    /// Rpc server which uses an IoT Edge module or device client to
    /// receive direct method invocations
    /// </summary>
    public sealed class IoTEdgeRpcServer : IRpcServer, IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "IoTEdge";

        /// <inheritdoc/>
        public IEnumerable<IRpcHandler> Connected => _listeners.Select(l => l.Server);

        /// <summary>
        /// Create connection
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public IoTEdgeRpcServer(IIoTEdgeDeviceClient client, ISerializer serializer,
            ILogger<IoTEdgeRpcServer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _listeners = ImmutableHashSet<Listener>.Empty;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var listeners = _listeners;
            _listeners = ImmutableHashSet<Listener>.Empty;

            foreach (var listener in listeners)
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask<IAsyncDisposable> ConnectAsync(IRpcHandler server,
            CancellationToken ct)
        {
            var listener = new Listener(server, this);
            bool add;
            lock (_lock)
            {
                add = _listeners.Count == 0;
                _listeners = _listeners.Add(listener);
            }
            if (add || !_registered)
            {
                try
                {
                    await _client.SetMethodHandlerAsync((request, _) => InvokeMethodAsync(request),
                        this, ct).ConfigureAwait(false);
                    _registered = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register method handler.");
                    _registered = false;
                }
            }
            return listener;
        }

        /// <summary>
        /// Remove from listener list
        /// </summary>
        /// <param name="listener"></param>
        /// <returns></returns>
        internal async ValueTask RemoveAsync(Listener listener)
        {
            bool remove;
            lock (_lock)
            {
                _listeners = _listeners.Remove(listener);
                remove = _listeners.Count == 0;
            }
            if (remove && _registered)
            {
                try
                {
                    await _client.SetMethodHandlerAsync(null, null).ConfigureAwait(false);
                    _registered = false;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to unregister method handler.");
                }
            }
        }

        /// <summary>
        /// Invoke method handler on method router
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private async Task<MethodResponse> InvokeMethodAsync(MethodRequest request)
        {
            using var cts = new CancellationTokenSource(request.ResponseTimeout
                ?? TimeSpan.FromSeconds(60));
            try
            {
                foreach (var listener in _listeners)
                {
                    var response = await listener.InvokeAsync(request,
                        cts.Token).ConfigureAwait(false);
                    if (response != null)
                    {
                        return response;
                    }
                }
                return new MethodResponse((int)HttpStatusCode.InternalServerError);
            }
            catch (OperationCanceledException)
            {
                return new MethodResponse((int)HttpStatusCode.RequestTimeout);
            }
        }

        /// <summary>
        /// Active listener
        /// </summary>
        internal sealed class Listener : IAsyncDisposable
        {
            /// <summary>
            /// Listening server
            /// </summary>
            public IRpcHandler Server { get; }

            /// <summary>
            /// Create listener
            /// </summary>
            /// <param name="server"></param>
            /// <param name="outer"></param>
            public Listener(IRpcHandler server, IoTEdgeRpcServer outer)
            {
                _outer = outer;
                Server = server;
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                return _outer.RemoveAsync(this);
            }

            /// <summary>
            /// Invoke method handler on method router
            /// </summary>
            /// <param name="request"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public async ValueTask<MethodResponse?> InvokeAsync(
                MethodRequest request, CancellationToken ct)
            {
                try
                {
                    var result = await Server.InvokeAsync(request.Name,
                        request.Data, ContentMimeType.Json, ct).ConfigureAwait(false);
                    if (result.Length > kMaxMessageSize)
                    {
                        _outer._logger.LogError("Result (Payload too large => {Length}",
                            result.Length);
                        return new MethodResponse(
                            (int)HttpStatusCode.RequestEntityTooLarge);
                    }
                    return new MethodResponse(result.ToArray(), 200);
                }
                catch (MethodCallStatusException mex)
                {
                    var payload = mex.Serialize(_outer._serializer);
                    return new MethodResponse(payload.Length > kMaxMessageSize ? null :
                        payload.ToArray(), mex.Details.Status ?? 500);
                }
                catch (NotSupportedException)
                {
                    return null;
                }
                catch (Exception)
                {
                    return new MethodResponse(
                        (int)HttpStatusCode.MethodNotAllowed);
                }
            }

            private readonly IoTEdgeRpcServer _outer;
        }

        private const int kMaxMessageSize = 127 * 1024;
        private readonly IIoTEdgeDeviceClient _client;
        private readonly ILogger _logger;
        private readonly ISerializer _serializer;
        private bool _registered;
        private ImmutableHashSet<Listener> _listeners;
        private readonly object _lock = new();
    }
}
