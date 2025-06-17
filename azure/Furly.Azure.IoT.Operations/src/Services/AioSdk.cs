// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.StateStore;
    using k8s.KubeConfigModels;
    using System;

    /// <summary>
    /// Wraps the aio sdk concept
    /// </summary>
    public sealed class AioSdk : IAioSdk, IDisposable
    {
        /// <summary>
        /// Create sdk
        /// </summary>
        /// <param name="context"></param>
        public AioSdk(ApplicationContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public IAdrClientWrapper CreateAdrClientWrapper(IMqttPubSubClient client)
        {
            var clientId = client.ClientId ?? throw new ArgumentException("Must have client id");
            return new AdrClientWrapper(_context, client, clientId);
        }

        /// <inheritdoc/>
        public IAdrServiceClient CreateAdrServiceClient(IMqttPubSubClient client)
        {
            var clientId = client.ClientId ?? throw new ArgumentException("Must have client id");
            return new AdrServiceClient(_context, client, clientId);
        }

        /// <inheritdoc/>
        public IStateStoreClient CreateStateStoreClient(IMqttPubSubClient client)
        {
            return new StateStoreClient(_context, client);
        }

        /// <inheritdoc/>
        public ISchemaRegistryClient CreateSchemaRegistryClient(IMqttPubSubClient client)
        {
            return new SchemaRegistryClient(_context, client);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _context.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private readonly ApplicationContext _context;
    }
}
