// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Azure.IoT.Operations.Runtime;
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.StateStore;
    using global::Azure.Iot.Operations.Services.LeaderElection;
    using k8s.KubeConfigModels;
    using System;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Wraps the aio sdk concept
    /// </summary>
    public sealed class AioSdk : IAioSdk, IDisposable
    {
        /// <summary>
        /// Create sdk
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        public AioSdk(ApplicationContext context, IOptions<AioOptions> options)
        {
            _context = context;
            _options = options;
        }

        /// <inheritdoc/>
        public IAdrClientWrapper CreateAdrClientWrapper(IMqttPubSubClient client)
        {
            return new AdrClientWrapper(_context, client);
        }

        /// <inheritdoc/>
        public IAdrServiceClient CreateAdrServiceClient(IMqttPubSubClient client)
        {
            return new AdrServiceClient(_context, client);
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
        public ILeaderElectionClient CreateLeaderElectionClient(IMqttPubSubClient client)
        {
            var id = _options.Value.Identity ?? _options.Value.ConnectorId ??
                throw new ArgumentException("Identity or ConnectorId must be set in options");
            return new LeaderElectionClient(_context, client, id, _options.Value.Name)
            {
                AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions
                {
                    AutomaticRenewal = true,
                    ElectionTerm = _options.Value.LeadershipTermLength,
                    RenewalPeriod = _options.Value.LeadershipRenewalPeriod
                }
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _context.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private readonly ApplicationContext _context;
        private readonly IOptions<AioOptions> _options;
    }
}
