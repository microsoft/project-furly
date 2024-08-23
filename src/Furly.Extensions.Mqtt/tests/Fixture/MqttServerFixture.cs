// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit;
    using Xunit.Abstractions;

    [CollectionDefinition(Name)]
    public class MqttCollection : ICollectionFixture<MqttServerFixture>
    {
        public const string Name = "Server";
    }

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
                builder.AddNewtonsoftJsonSerializer();
                builder.AddMqttServer();
                builder.RegisterInstance(sink.ToLoggerFactory())
                    .As<ILoggerFactory>();
                builder.AddLogging();
                _container = builder.Build();

                _server = _container.Resolve<IAwaitable<MqttServer>>().GetAwaiter().GetResult();
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

        public IRpcClient? RpcClient => _server;

        public IRpcServer? RpcServer => _server;

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
