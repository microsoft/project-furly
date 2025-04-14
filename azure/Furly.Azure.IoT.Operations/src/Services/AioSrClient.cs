// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Messaging;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
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
        /// <param name="context"></param>
        /// <param name="client"></param>
        public AioSrClient(ApplicationContext context, IMqttPubSubClient client)
        {
            _client = new SchemaRegistryClient(context, client);
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
                ContentMimeType.JsonSchema => Format.JsonSchemaDraft07,
                _ => throw new NotSupportedException($"{schema.Type} type not supported")
            };
            var result = await _client.PutAsync(schema.Schema, schemaType,
                cancellationToken: ct).ConfigureAwait(false);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to register schema {schema.Name}");
            }
            return $"{result.Namespace}/{result.Name}";
        }

        private readonly SchemaRegistryClient _client;
    }
}
