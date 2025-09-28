// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging.Clients
{
    using Autofac;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Messaging.Runtime;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Create event clients for the filesystem
    /// </summary>
    public sealed class FileSystemClientFactory : IEventClientFactory, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "FileSystem";

        /// <summary>
        /// Create event client factory
        /// </summary>
        /// <param name="scope"></param>
        public FileSystemClientFactory(ILifetimeScope scope)
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
            lock (_clients)
            {
                if (!_clients.TryGetValue(Name, out var refCountedScope))
                {
                    refCountedScope = new RefCountedClientScope(this, connectionString);
                    _clients.Add(connectionString, refCountedScope);
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
        private sealed class RefCountedClientScope :
            IPostConfigureOptions<FileSystemEventClientOptions>, IDisposable
        {
            /// <summary>
            /// Scope for the client
            /// </summary>
            public ILifetimeScope Scope { get; }

            /// <summary>
            /// Create a reference counted scope
            /// </summary>
            public RefCountedClientScope(FileSystemClientFactory outer, string connectionString)
            {
                _outer = outer;
                _connectionString = connectionString;
                Scope = _outer._scope.BeginLifetimeScope(builder =>
                {
                    builder.AddFileSystemEventClient();
                    builder.RegisterInstance(this)
                        .As<IPostConfigureOptions<FileSystemEventClientOptions>>()
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
            public void PostConfigure(string? name, FileSystemEventClientOptions options)
            {
                options.OutputFolder = _connectionString;
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

            private readonly FileSystemClientFactory _outer;
            private readonly string _connectionString;
            private int _refCount;
        }

        private readonly ILifetimeScope _scope;
        private readonly Dictionary<string, RefCountedClientScope> _clients
            = new(StringComparer.OrdinalIgnoreCase);
    }
}
