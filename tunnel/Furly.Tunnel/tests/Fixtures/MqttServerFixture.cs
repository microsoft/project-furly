// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Mqtt.Clients;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class MqttServerFixture : IDisposable
    {
        public bool Up { get; private set; }

        /// <summary>
        /// Create fixture
        /// </summary>
        public MqttServerFixture(IMessageSink sink)
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.AddMqttServer();
                builder.RegisterInstance(sink.ToLoggerFactory())
                    .As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();

                _server = _container.Resolve<MqttServer>().GetAwaiter().GetResult();
                Up = true;
            }
            catch
            {
                _server = null;
                _container = null;
            }
        }

        public IEventSubscriber? Subscriber => _server;

        public IEventClient? Publisher => _server;

        /// <inheritdoc/>
        public void Dispose()
        {
            _server?.Dispose();
            _container?.Dispose();
            Up = false;
        }

        private readonly MqttServer? _server;
        private readonly IContainer? _container;
    }
}
