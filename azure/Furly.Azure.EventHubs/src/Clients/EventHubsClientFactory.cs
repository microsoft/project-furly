// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs.Clients
{
    using Autofac;
    using Furly.Extensions.Messaging;
    using System;

    /// <summary>
    /// Create event clients for EventHubs
    /// </summary>
    public sealed class EventHubsClientFactory : IEventClientFactory, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "EventHub";

        /// <summary>
        /// Create event client factory
        /// </summary>
        /// <param name="scope"></param>
        public EventHubsClientFactory(ILifetimeScope scope)
        {
            _scope = scope;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _scope.Dispose();
        }

        /// <inheritdoc/>
        public IDisposable CreateEventClient(string connectionString,
            out IEventClient client)
        {
            var scope = _scope.BeginLifetimeScope(builder =>
            {
                builder.AddHubEventClient();
                builder.Configure<EventHubsClientOptions>(
                    o => o.ConnectionString = connectionString);
            });
            client = scope.Resolve<IEventClient>();
            return scope;
        }

        private readonly ILifetimeScope _scope;
    }
}
