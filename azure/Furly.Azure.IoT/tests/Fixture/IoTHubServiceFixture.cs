// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Azure.IoT.Runtime;
    using Autofac;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Threading;
    using Xunit;

    [CollectionDefinition(Name)]
    public class IoTHubServiceCollection : ICollectionFixture<IoTHubServiceFixture>
    {
        public const string Name = "Server";
    }

    public sealed class IoTHubServiceFixture : IDisposable
    {
        public static bool Up => _container != null;

        /// <summary>
        /// Create fixture
        /// </summary>
        public IoTHubServiceFixture()
        {
            if (Interlocked.Increment(ref _refcount) == 1)
            {
                try
                {
                    // Read connections string from keyvault
                    var config = new ConfigurationBuilder()
                        .AddFromDotEnvFile()
                        .Build();

                    var builder = new ContainerBuilder();
                    builder.AddConfiguration(config);
                    builder.AddIoTHubRpcClient();
                    builder.AddIoTHubEventSubscriber();
                    builder.RegisterType<IoTHubEventProcessorConfig>()
                        .AsImplementedInterfaces(); // Point to iot hub
                    builder.AddOptions();
                    builder.AddLogging();
                    _container = builder.Build();
                }
                catch
                {
                    Interlocked.Decrement(ref _refcount);
                    _container = null;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Decrement(ref _refcount) == 0)
            {
                _container?.Dispose();
                _container = null;
            }
        }

        private static IContainer _container;
        private static int _refcount;
    }
}
