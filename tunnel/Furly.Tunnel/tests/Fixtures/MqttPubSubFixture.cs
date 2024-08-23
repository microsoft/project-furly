// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class MqttPubSubFixture : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        public MqttPubSubFixture(MqttServerFixture server, ITestOutputHelper output)
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
                publisher.AddNewtonsoftJsonSerializer();
                publisher.AddMqttClient();
                publisher.Configure<MqttOptions>(options => options.ClientId = "Publisher");
                publisher.RegisterInstance(output.ToLoggerFactory()).As<ILoggerFactory>();
                publisher.AddLogging();
                _publisher = publisher.Build();

                var subscriber = new ContainerBuilder();
                subscriber.AddNewtonsoftJsonSerializer();
                subscriber.AddMqttClient();
                subscriber.Configure<MqttOptions>(options => options.ClientId = "Subscriber");
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
