// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Clients
{
    using Furly.Extensions.Messaging;
    using global::Azure.Data.SchemaRegistry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Schema registry in event hub
    /// </summary>
    public sealed class SchemaGroup : SchemaRegistryBase
    {
        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        public SchemaGroup(IOptions<SchemaRegistryOptions> options,
            ICredentialProvider credential, ILogger<SchemaGroup> logger)
            : this(options.Value, credential, logger)
        {
        }

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        internal SchemaGroup(SchemaRegistryOptions options, ICredentialProvider credential,
            ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schemaGroupName = options.SchemaGroupName;

            _schemaRegistry = new SchemaRegistryClient(options.FullyQualifiedNamespace,
                credential.Credential);
        }

        /// <inheritdoc/>
        protected override async ValueTask<string> RegisterAsync(IEventSchema schema,
            string schemaString, CancellationToken ct)
        {
            var schemaProperties = await _schemaRegistry.RegisterSchemaAsync(
                _schemaGroupName, schema.Name, schemaString, schema.Type,
                ct).ConfigureAwait(false);

            _logger.LogInformation("Schema {Name} registered successfully.", schema.Name);
            return schemaProperties.Value.Id ?? string.Empty;
        }

        private readonly SchemaRegistryClient _schemaRegistry;
        private readonly ILogger _logger;
        private readonly string _schemaGroupName;
    }
}
