// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr
{
    using Furly.Extensions.Messaging;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed record class EventConsumerArg(string Target, byte[] Data, string ContentType,
        IReadOnlyDictionary<string, string?> Properties, IEventClient? Responder);

    internal sealed class CallbackConsumer : IEventConsumer
    {
        internal CallbackConsumer(
            Action<EventConsumerArg> handler)
        {
            _handler = handler;
        }

        public Task HandleAsync(string source, ReadOnlyMemory<byte> data, string contentType,
            IReadOnlyDictionary<string, string?> properties, IEventClient? responder, CancellationToken ct)
        {
            _handler(new EventConsumerArg(source, data.ToArray(), contentType, properties, responder));
            return Task.CompletedTask;
        }
        private readonly Action<EventConsumerArg> _handler;
    }
}
