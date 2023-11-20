// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests
{
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// In memory connector from client to server
    /// </summary>
    public sealed class InMemoryRpcServer : IRpcClient, IRpcServer
    {
        /// <inheritdoc/>
        public IEnumerable<IRpcHandler> Connected =>
            _server?.YieldReturn() ?? Enumerable.Empty<IRpcHandler>();

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes => 120 * 1024;

        /// <inheritdoc/>
        public string Name => "Test";

        /// <inheritdoc/>
        public ValueTask<IAsyncDisposable> ConnectAsync(IRpcHandler server,
            CancellationToken ct)
        {
            _server = server;
#pragma warning disable CA2000 // Dispose objects before losing scope
            var disconnect = new Disconnect(this);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return ValueTask.FromResult<IAsyncDisposable>(disconnect);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout, CancellationToken ct)
        {
            if (_server == null)
            {
                throw new ExternalDependencyException("Not connected");
            }
            var result = await _server.InvokeAsync(method, payload, contentType, ct).ConfigureAwait(false);
            const int kMaxMessageSize = 127 * 1024;
            return result.Length > kMaxMessageSize
                ? throw new MethodCallStatusException(
                    (int)HttpStatusCode.RequestEntityTooLarge)
                : result;
        }

        private sealed class Disconnect : IAsyncDisposable
        {
            public Disconnect(InMemoryRpcServer outer)
            {
                _outer = outer;
            }
            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                _outer._server = null;
                return ValueTask.CompletedTask;
            }
            private readonly InMemoryRpcServer _outer;
        }

        private IRpcHandler? _server;
    }
}
