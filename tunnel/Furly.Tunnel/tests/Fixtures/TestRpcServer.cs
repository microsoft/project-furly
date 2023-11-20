// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel
{
    using Furly.Tunnel.Protocol;
    using Furly;
    using Furly.Extensions.Logging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class TestRpcServer : IRpcClient, IRpcServer
    {
        /// <inheritdoc/>
        public IEnumerable<IRpcHandler> Connected =>
            _handler?.YieldReturn() ?? Enumerable.Empty<IRpcHandler>();

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes { get; }

        /// <inheritdoc/>
        public string Name => "Test";

        public TestRpcServer(IJsonSerializer serializer, int size = 128 * 1024,
            Action<string, string, byte[], string>? callback = null)
        {
            MaxMethodPayloadSizeInBytes = size;
            _callback = callback ?? ((_, _, _, _) => { });
            _serializer = serializer;
        }

        /// <summary>
        /// Create a method client to the server
        /// </summary>
        /// <returns></returns>
        public IMethodClient CreateClient()
        {
            return new ChunkMethodClient(this, _serializer, Log.Console<ChunkMethodClient>());
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout, CancellationToken ct)
        {
            if (method != "$call")
            {
                _callback.Invoke(target, method, payload.ToArray(), contentType);
            }
            if (_handler == null)
            {
                throw new InvalidOperationException("Not connected");
            }
            return await _handler.InvokeAsync(method, payload, contentType,
                ct).ConfigureAwait(false);
        }

        public ValueTask<IAsyncDisposable> ConnectAsync(IRpcHandler server,
            CancellationToken ct)
        {
            System.Diagnostics.Debug.Assert(_handler == null);
            _handler = server;
#pragma warning disable CA2000 // Dispose objects before losing scope
            var disconnect = new Disconnect(() => _handler = null);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return ValueTask.FromResult<IAsyncDisposable>(disconnect);
        }

        private sealed class Disconnect : IAsyncDisposable
        {
            public Disconnect(Action action)
            {
                _action = action;
            }
            public ValueTask DisposeAsync()
            {
                _action();
                return ValueTask.CompletedTask;
            }
            private readonly Action _action;
        }

        private IRpcHandler? _handler;
        private readonly Action<string, string, byte[], string> _callback;
        private readonly IJsonSerializer _serializer;
    }
}
