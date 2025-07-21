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
    using Microsoft.Extensions.Logging;
    using Nito.Disposables;
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aio schema registry client
    /// </summary>
    public sealed class AioSrClient : SchemaRegistryBase, IAioSrClient, IDisposable
    {
        /// <summary>
        /// Create aio sr client
        /// </summary>
        /// <param name="sdk"></param>
        /// <param name="client"></param>
        /// <param name="logger"></param>
        public AioSrClient(IAioSdk sdk, IMqttPubSubClient client, ILogger<AioSrClient> logger)
        {
            _client = sdk.CreateSchemaRegistryClient(client);
            _logger = logger;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public IDisposable Register(IAioSrCallbacks callbacks)
        {
            var token = Guid.NewGuid().ToString();
            var registration = new Disposable(() => _events.TryRemove(token, out _));
            if (!_events.TryAdd(token, callbacks))
            {
                throw new InvalidOperationException($"Failed to register events for {token}");
            }
            return registration;
        }

        /// <inheritdoc/>
        protected override async ValueTask<string> RegisterAsync(IEventSchema schema,
            string schemaString, CancellationToken ct)
        {
            var schemaType = schema.Type switch
            {
                ContentMimeType.JsonSchema => Format.JsonSchemaDraft07,
                _ => throw new NotSupportedException($"{schema.Type} type not supported")
            };
            var result = await _client.PutAsync(schemaString, schemaType,
                version: ((int)schema.Version).ToString(),
                // tags: new Dictionary<string, string> { { "schemaId", schema.Name } },
                cancellationToken: ct).ConfigureAwait(false);
            if (result?.Name == null)
            {
                throw new InvalidOperationException(
                    $"Failed to register schema {schema.Name}:{schema.Version}");
            }
            foreach (var cb in _events.Values)
            {
                await cb.OnSchemaRegisteredAsync(schema, result, ct).ConfigureAwait(false);
            }
            _logger.SchemaRegistered(schema.Name, result.Name, result.Namespace);
            return result.Name;
        }

        private readonly ISchemaRegistryClient _client;
        private readonly ILogger<AioSrClient> _logger;
        private readonly ConcurrentDictionary<string, IAioSrCallbacks> _events = new();
    }

    /// <summary>
    /// Source-generated logging for AioSrClient
    /// </summary>
    internal static partial class AioSrClientLogging
    {
        private const int EventClass = 90;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Schema '{SchemaName}' registered with ID '{SchemaId}' in namespace '{Namespace}'")]
        public static partial void SchemaRegistered(this ILogger logger, string? schemaName,
            string schemaId, string? @namespace);
    }
}
