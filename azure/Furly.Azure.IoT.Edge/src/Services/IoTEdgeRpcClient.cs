// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure.IoT.Edge;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Azure.Devices.Client;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Method client
    /// </summary>
    public sealed class IoTEdgeRpcClient : IRpcClient
    {
        /// <inheritdoc/>
        public string Name => "IoTEdge";

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes => 120 * 1024;

        /// <summary>
        /// Create method client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        public IoTEdgeRpcClient(IIoTEdgeDeviceClient client, ISerializer serializer)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout, CancellationToken ct)
        {
            if (!HubResource.Parse(target, out _, out var deviceId,
                out var moduleId, out var error))
            {
                throw new ArgumentException($"Invalid target {target} provided ({error})");
            }
            var request = new MethodRequest(method, payload.ToArray(), timeout, null);
            MethodResponse response;
            if (string.IsNullOrEmpty(moduleId))
            {
                response = await _client.InvokeMethodAsync(deviceId, request,
                    ct).ConfigureAwait(false);
            }
            else
            {
                response = await _client.InvokeMethodAsync(deviceId, moduleId,
                    request, ct).ConfigureAwait(false);
            }
            if (response.Status != 200)
            {
                MethodCallStatusException.TryThrow(response.Result.AsMemory(),
                    _serializer, response.Status);
            }
            return response.Result;
        }

        private readonly ISerializer _serializer;
        private readonly IIoTEdgeDeviceClient _client;
    }
}
