// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.RabbitMq.Server;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit;
    using Xunit.Abstractions;

    [CollectionDefinition(Name)]
    public class RabbitMqCollection : ICollectionFixture<RabbitMqServerFixture>
    {
        public const string Name = "RabbitMqServer";
    }

    public sealed class RabbitMqServerFixture : IDisposable
    {
        public bool Up { get; private set; }

        /// <summary>
        /// Create fixture
        /// </summary>
        public RabbitMqServerFixture(IMessageSink sink)
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.AddRabbitMqQueueClient(); // Health check and config
                builder.RegisterType<RabbitMqServer>()
                    .AsSelf().SingleInstance();
                builder.RegisterInstance(sink.ToLoggerFactory())
                    .As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();

               // _server = _container.Resolve<RabbitMqServer>();
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

        private readonly RabbitMqServer? _server;
        private readonly IContainer? _container;
    }
}
