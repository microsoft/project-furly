// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class MqttClientHarness : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        public MqttClientHarness(MqttServerFixture server, ITestOutputHelper output, MqttVersion version)
        {
            if (!server.Up)
            {
                _publisher = null;
                _subscriber = null;
                return;
            }
            try
            {
                var publisher = new ContainerBuilder();
                publisher.AddMqttClient(options =>
                {
                    options.ClientId = "Publisher";
                    options.Protocol = version;
                    options.NumberOfClientPartitions = 3;
                });
                publisher.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                publisher.AddLogging();
                _publisher = publisher.Build();

                var subscriber = new ContainerBuilder();
                subscriber.AddMqttClient(options =>
                {
                    options.ClientId = "Subscriber";
                    options.Protocol = version;
                    options.NumberOfClientPartitions = 3;
                });
                subscriber.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                subscriber.AddLogging();
                _subscriber = subscriber.Build();
            }
            catch
            {
                _subscriber = null;
                _publisher = null;
            }
        }

        /// <summary>
        /// Get Event Subscriber
        /// </summary>
        public IEventSubscriber? GetSubscriberEventSubscriber()
        {
            return Try.Op(() => _subscriber?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventClient? GetPublisherEventClient()
        {
            return Try.Op(() => _publisher?.Resolve<IEventClient>());
        }

        /// <summary>
        /// Get Event client
        /// </summary>
        public IEventSubscriber? GetPublisherEventSubscriber()
        {
            return Try.Op(() => _publisher?.Resolve<IEventSubscriber>());
        }

        /// <summary>
        /// Get Rpc client
        /// </summary>
        public IRpcClient? GetRpcClient()
        {
            return Try.Op(() => _publisher?.Resolve<IRpcClient>());
        }

        /// <summary>
        /// Get Rpc server
        /// </summary>
        public IRpcServer? GetRpcServer()
        {
            return Try.Op(() => _subscriber?.Resolve<IRpcServer>());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _publisher?.Dispose();
            _subscriber?.Dispose();
        }

        private readonly IContainer? _publisher;
        private readonly IContainer? _subscriber;
    }
}
