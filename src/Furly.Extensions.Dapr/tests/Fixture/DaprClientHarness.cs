// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Storage;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;
    using Xunit.Abstractions;

    public sealed class DaprClientHarness : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        public DaprClientHarness(ITestOutputHelper output)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var port = Random.Shared.Next(8000, 9000);
#pragma warning restore CA5394 // Do not use insecure randomness
            _connector = new DaprSidecarConnector(port);

            var builder = new ContainerBuilder();
            builder.AddDaprPubSubClient();
            builder.AddDaprStateStoreClient();
            builder.Configure<DaprOptions>(options =>
            {
                options.GrpcEndpoint = $"http://localhost:{_connector.Port}";
                options.HttpEndpoint = $"http://localhost:{_connector.Port}";
                options.PubSubComponent = "Test";
            });
            builder.RegisterInstance(_connector)
                .As<IEventSubscriber>()
                .As<IDaprSidecarStorage>().ExternallyOwned();
            builder.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
            builder.AddLogging();
            _container = builder.Build();
        }

        /// <summary>
        /// Get Event Subscriber
        /// </summary>
        public IEventSubscriber GetEventSubscriber()
        {
            return _container.Resolve<IEventSubscriber>();
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient GetEventClient()
        {
            return _container.Resolve<IEventClient>();
        }

        /// <summary>
        /// Get key value store
        /// </summary>
        public async Task<IKeyValueStore> GetKeyValueStoreAsync()
        {
            return await _container.Resolve<IAwaitable<IKeyValueStore>>();
        }

        /// <summary>
        /// Get side car store
        /// </summary>
        public IDaprSidecarStorage GetSidecarStorage()
        {
            return _container.Resolve<IDaprSidecarStorage>();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _container.Dispose();
            _connector.Dispose();
        }

        private readonly IContainer _container;
        private readonly DaprSidecarConnector _connector;
    }
}
