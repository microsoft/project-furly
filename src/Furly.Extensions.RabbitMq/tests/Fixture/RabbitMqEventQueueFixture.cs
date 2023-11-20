// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.RabbitMq.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class RabbitMqEventQueueHarness : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        public RabbitMqEventQueueHarness(RabbitMqServerFixture server, string queue, ITestOutputHelper output)
        {
            if (!server.Up)
            {
                _client = null;
                _server = null;
            }
            try
            {
                var clientBuilder = new ContainerBuilder();
                clientBuilder.AddRabbitMqQueueClient();
                clientBuilder.Configure<RabbitMqQueueOptions>(options => options.Queue = queue);
                clientBuilder.RegisterType<RabbitMqConfig>()
                    .AsImplementedInterfaces().SingleInstance();
                clientBuilder.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                clientBuilder.AddLogging();
                _client = clientBuilder.Build();

                var serverBuilder = new ContainerBuilder();
                serverBuilder.AddRabbitMqQueueClient();
                serverBuilder.Configure<RabbitMqQueueOptions>(options => options.Queue = queue);
                serverBuilder.RegisterType<RabbitMqConfig>()
                    .AsImplementedInterfaces().SingleInstance();
                serverBuilder.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                serverBuilder.AddLogging();
                _server = serverBuilder.Build();
            }
            catch
            {
                _client = null;
                _server = null;
            }
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventSubscriber? GetEventSubscriber()
        {
            return Try.Op(() => _server?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient? GetEventClient()
        {
            return Try.Op(() => _client?.Resolve<IEventClient>());
        }

        /// <summary>
        /// Clean up query container
        /// </summary>
        public void Dispose()
        {
            _client?.Dispose();
            _server?.Dispose();
        }

        private readonly IContainer? _client;
        private readonly IContainer? _server;
    }
}
