// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Messaging;
    using global::Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Callbacks for the Aio schema registry client
    /// </summary>
    public interface IAioSrCallbacks
    {
        /// <summary>
        /// Called when a schema is registered
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="registration"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask OnSchemaRegisteredAsync(IEventSchema schema, Schema registration,
            CancellationToken ct = default);
    }
}
