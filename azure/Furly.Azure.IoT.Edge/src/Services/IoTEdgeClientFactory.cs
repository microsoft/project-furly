// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Autofac;
    using Furly.Azure.IoT.Edge;
    using Furly.Extensions.Messaging;
    using System;

    /// <summary>
    /// Create event clients for IoT Edge
    /// </summary>
    public sealed class IoTEdgeClientFactory : IEventClientFactory, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "IoTEdge";

        /// <summary>
        /// Create event client factory
        /// </summary>
        /// <param name="scope"></param>
        public IoTEdgeClientFactory(ILifetimeScope scope)
        {
            _scope = scope;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _scope.Dispose();
        }

        /// <inheritdoc/>
        public IDisposable CreateEventClientWithConnectionString(string connectionString,
            out IEventClient client)
        {
            var scope = _scope.BeginLifetimeScope(builder =>
            {
                builder.AddIoTEdgeServices();
                builder.Configure<IoTEdgeClientOptions>(
                    o => o.EdgeHubConnectionString = connectionString);
            });
            client = scope.Resolve<IEventClient>();
            return scope;
        }

        private readonly ILifetimeScope _scope;
    }
}
