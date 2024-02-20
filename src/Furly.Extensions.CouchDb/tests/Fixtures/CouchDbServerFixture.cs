// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.CouchDb.Server;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit;
    using Xunit.Abstractions;

    [CollectionDefinition(Name)]
    public class CouchDbServerCollection : ICollectionFixture<CouchDbServerFixture>
    {
        public const string Name = "Server";
    }

    public sealed class CouchDbServerFixture : IDisposable
    {
        public bool Up { get; private set; }

        /// <summary>
        /// Create fixture
        /// </summary>
        public CouchDbServerFixture(IMessageSink sink)
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.AddCouchDbClient(); // Health check and config
                builder.RegisterType<CouchDbServer>()
                    .AsSelf().SingleInstance();
                builder.RegisterInstance(sink.ToLoggerFactory())
                    .As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();
              //  _server = _container.Resolve<CouchDbServer>();
              //  _server.StartAsync().GetAwaiter().GetResult();
              //  Up = true;
            }
            catch (Exception)
            {
                _server = null;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _server?.Dispose();
            _container?.Dispose();
            Up = false;
        }

        private readonly CouchDbServer? _server;
        private readonly IContainer? _container;
    }
}
