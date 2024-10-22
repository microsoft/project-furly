// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Messaging;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio schema registry client
    /// </summary>
    public sealed class AioSrClient : ISchemaRegistry, IDisposable
    {
        /// <summary>
        /// Create aio sr client
        /// </summary>
        /// <param name="client"></param>
        public AioSrClient(IMqttPubSubClient client)
        {
            _client = new SchemaRegistryClient(client);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask<string> RegisterAsync(IEventSchema schema, CancellationToken ct)
        {
            var schemaType = schema.Type switch
            {
                ContentMimeType.JsonSchema => Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07,
                _ => throw new NotSupportedException($"{schema.Type} type not supported")
            };
            var result = await _client.PutAsync(schema.Schema, schemaType,
                cancellationToken: ct).ConfigureAwait(false);
            return $"{result.Namespace}/{result.Name}";
        }

        private readonly SchemaRegistryClient _client;
    }
}
