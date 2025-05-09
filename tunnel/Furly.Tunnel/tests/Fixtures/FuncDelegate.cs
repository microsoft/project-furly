﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Protocol
{
    using Furly.Extensions.Rpc;
    using System;
    using System.Buffers;
    using System.Threading;
    using System.Threading.Tasks;

    public class FuncDelegate : IRpcHandler
    {
        /// <inheritdoc/>
        public string MountPoint { get; }

        /// <inheritdoc/>
        public FuncDelegate(string mountPoint,
            Func<string, byte[], string, CancellationToken, byte[]> handler)
        {
            MountPoint = mountPoint;
            _handler = handler;
        }

        /// <inheritdoc/>
        public ValueTask<ReadOnlySequence<byte>> InvokeAsync(string method,
            ReadOnlySequence<byte> payload, string contentType, CancellationToken ct)
        {
            return ValueTask.FromResult<ReadOnlySequence<byte>>(new ReadOnlySequence<byte>(
                _handler.Invoke(method, payload.ToArray(), contentType, ct)));
        }

        private readonly Func<string, byte[], string, CancellationToken, byte[]> _handler;
    }
}
