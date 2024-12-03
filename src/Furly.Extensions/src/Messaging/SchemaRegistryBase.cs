// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using Furly.Extensions.Utils;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Schema registry base
    /// </summary>
    public abstract class SchemaRegistryBase : ISchemaRegistry
    {
        /// <inheritdoc/>
        public async ValueTask<string> RegisterAsync(IEventSchema schema,
            CancellationToken ct)
        {
            // Check the cache
            var key = schema.Name + schema.Version;
            if (_schemaToIdMap.TryGet(key, out var value))
            {
                return value;
            }

            // TODO: versioning
            var schemaString = schema.Schema;
            var id = await RegisterAsync(schema, schemaString, ct).ConfigureAwait(false);
            _schemaToIdMap.AddOrUpdate(key, id, schemaString.Length);
            return id;
        }

        /// <summary>
        /// Register directly with registry
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="schemaString"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract ValueTask<string> RegisterAsync(IEventSchema schema,
            string schemaString, CancellationToken ct);

        private const int kCacheCapacity = 128;
        private readonly LruCache<string, string> _schemaToIdMap = new(kCacheCapacity);
    }
}
