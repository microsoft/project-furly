// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
#nullable enable
namespace Furly.Azure.IoT.Services
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed record class IoTHubTelemetryHandlerArg(string DeviceId, string? ModuleId,
        string Topic, byte[] Data, string ContentType, string ContentEncoding,
        IReadOnlyDictionary<string, string?> Properties, int Count);

    internal sealed class IoTHubTelemetryHandler : IIoTHubTelemetryHandler
    {
        internal IoTHubTelemetryHandler(
            Action<IoTHubTelemetryHandlerArg> handler)
        {
            _handler = handler;
        }

        public ValueTask HandleAsync(string deviceId, string? moduleId, string topic,
            ReadOnlySequence<byte> data, string contentType, string contentEncoding,
            IReadOnlyDictionary<string, string?> properties, CancellationToken ct = default)
        {
            _count++;
            _handler(new IoTHubTelemetryHandlerArg(deviceId, moduleId, topic, data.ToArray(),
                contentType, contentEncoding, properties, _count));
            return ValueTask.CompletedTask;
        }

        private readonly Action<IoTHubTelemetryHandlerArg> _handler;
        private int _count;
    }
}
