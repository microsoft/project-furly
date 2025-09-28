// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Autofac;
    using Furly.Azure.IoT.Edge;
    using Furly.Extensions.Messaging;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;

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
        public IDisposable CreateEventClient(string connectionString,
            out IEventClient client)
        {
            // validate and create canonical form
            var cs = IotHubConnectionStringBuilder.Create(connectionString).ToString();
            lock (_clients)
            {
                if (!_clients.TryGetValue(Name, out var refCountedScope))
                {
                    refCountedScope = new RefCountedClientScope(this, cs);
                    _clients.Add(cs, refCountedScope);
                }
                refCountedScope.AddRef();
                client = refCountedScope.Scope.Resolve<IEventClient>();
                return refCountedScope;
            }
        }

        /// <summary>
        /// Create a client scope that is reference counted to share client
        /// across multiple consumers
        /// </summary>
        private sealed class RefCountedClientScope : IPostConfigureOptions<IoTEdgeClientOptions>,
            IDisposable
        {
            /// <summary>
            /// Scope for the client
            /// </summary>
            public ILifetimeScope Scope { get; }

            /// <summary>
            /// Create a reference counted scope
            /// </summary>
            public RefCountedClientScope(IoTEdgeClientFactory outer, string connectionString)
            {
                _outer = outer;
                _connectionString = connectionString;
                Scope = _outer._scope.BeginLifetimeScope(builder =>
                {
                    builder.AddIoTEdgeServices();
                    builder.RegisterInstance(this)
                        .As<IPostConfigureOptions<IoTEdgeClientOptions>>()
                        .SingleInstance()
                        .ExternallyOwned();
                });
            }

            /// <summary>
            /// Add a reference to the scope
            /// </summary>
            public void AddRef()
            {
                System.Threading.Interlocked.Increment(ref _refCount);
            }

            /// <inheritdoc/>
            public void PostConfigure(string? name, IoTEdgeClientOptions options)
            {
                options.EdgeHubConnectionString = _connectionString;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (System.Threading.Interlocked.Decrement(ref _refCount) == 0)
                {
                    lock (_outer._clients)
                    {
                        if (_outer._clients.Remove(_outer.Name, out var _))
                        {
                            Scope.Dispose();
                        }
                    }
                }
            }

            private readonly IoTEdgeClientFactory _outer;
            private readonly string _connectionString;
            private int _refCount;
        }

        private readonly ILifetimeScope _scope;
        private readonly Dictionary<string, RefCountedClientScope> _clients
            = new(StringComparer.OrdinalIgnoreCase);
    }
}
