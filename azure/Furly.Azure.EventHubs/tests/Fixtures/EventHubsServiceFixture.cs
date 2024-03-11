// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Tests.Fixtures
{
    using Furly.Azure.EventHubs.Runtime;
    using Autofac;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Threading;
    using Xunit;
    using Furly.Azure.IoT.Runtime;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using Furly.Azure.IoT;

    [CollectionDefinition(Name)]
    public class EventHubsServiceCollection : ICollectionFixture<EventHubsServiceFixture>
    {
        public const string Name = "Server";
    }

    public sealed class EventHubsServiceFixture : IDisposable
    {
        public bool Up => _container != null;

        /// <summary>
        /// Create fixture
        /// </summary>
        public EventHubsServiceFixture()
        {
            try
            {
                // Read connections string from keyvault
                var config = new ConfigurationBuilder()
                    .AddFromDotEnvFile()
                    .Build();

                var builder = new ContainerBuilder();
                builder.AddConfiguration(config);
                builder.AddHubEventClient();

                var cs = Environment.GetEnvironmentVariable("_EH_CS");
                if (string.IsNullOrEmpty(cs))
                {
                    return;
                }
                builder.Configure<EventHubsClientOptions>(
                    options => options.ConnectionString = cs);

                builder.AddIoTHubEventSubscriber();
                builder.RegisterType<IoTHubEventProcessorConfig>()
                    .AsImplementedInterfaces();
                builder.Configure<IoTHubServiceOptions>(
                    options => options.ConnectionString = cs);
                builder.AddOptions();
                builder.AddLogging();
                _container = builder.Build();
            }
            catch
            {
                _container = null;
            }
        }

        /// <summary>
        /// Get Event subscriber
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
            _container = null;
        }

        private IContainer? _container;
    }
}
