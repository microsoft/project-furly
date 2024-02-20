// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Kafka.Server;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit;
    using Xunit.Abstractions;

    [CollectionDefinition(Name)]
    public class KafkaCollection : ICollectionFixture<KafkaServerFixture>
    {
        public const string Name = "Server";
    }

    public sealed class KafkaServerFixture : IDisposable
    {
        public bool Up { get; private set; }

        /// <summary>
        /// Create fixture
        /// </summary>
        public KafkaServerFixture(IMessageSink sink)
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.AddKafkaAdminClient(); // Health check and config
                builder.RegisterType<KafkaCluster>()
                    .AsSelf().SingleInstance();

                builder.RegisterInstance(sink.ToLoggerFactory())
                    .As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();
              // _server = _container.Resolve<KafkaCluster>();
              // _server.StartAsync().GetAwaiter().GetResult();
              // Up = true;
            }
            catch
            {
                _server = null;
                _container = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _server?.Dispose();
            _container?.Dispose();
            Up = false;
        }

        private readonly IContainer? _container;
        private readonly KafkaCluster? _server;
    }
}
