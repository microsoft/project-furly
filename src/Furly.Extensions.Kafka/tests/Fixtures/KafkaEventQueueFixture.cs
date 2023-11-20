// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Utils;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using System;
    using Xunit.Abstractions;

    public sealed class KafkaEventQueueHarness : IDisposable
    {
        /// <summary>
        /// Create fixture
        /// </summary>
        internal KafkaEventQueueHarness(KafkaServerFixture server, string topic, ITestOutputHelper output)
        {
            if (!server.Up)
            {
                _container = null;
                return;
            }
            try
            {
                var builder = new ContainerBuilder();

                builder.AddKafkaConsumerClient();
                builder.AddKafkaProducerClient();

                builder.Configure<KafkaProducerOptions>(options =>
                    options.Topic = topic);

                builder.Configure<KafkaConsumerOptions>(options =>
                {
                    options.CheckpointInterval = TimeSpan.FromMinutes(1);
                    options.InitialReadFromEnd = false;
                    options.ConsumerGroup = "$default";
                    options.ConsumerTopic = topic;
                });

                builder.RegisterType<ProcessIdentityMock>()
                    .AsImplementedInterfaces();
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

        /// <summary>
        /// Clean up query container
        /// </summary>
        public void Dispose()
        {
            _container?.Dispose();
        }

        private readonly IContainer? _container;
    }
}
