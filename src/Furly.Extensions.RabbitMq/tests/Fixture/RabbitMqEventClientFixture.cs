// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class RabbitMqEventClientHarness : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        public RabbitMqEventClientHarness(RabbitMqServerFixture server, ITestOutputHelper output)
        {
            if (!server.Up)
            {
                _container = null;
                return;
            }
            try
            {
                var builder = new ContainerBuilder();
                builder.AddRabbitMqBrokerClient();
                builder.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();
            }
            catch
            {
                _container = null;
            }
        }

        /// <summary>
        /// Get Event Subscriber
        /// </summary>
        public IEventSubscriber? GetEventSubscriber()
        {
            return Try.Op(() => _container?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient? GetEventClient()
        {
            return Try.Op(() => _container?.Resolve<IEventClient>());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _container?.Dispose();
        }

        private readonly IContainer? _container;
    }
}
