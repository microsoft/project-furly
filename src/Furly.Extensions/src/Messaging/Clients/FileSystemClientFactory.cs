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
            var scope = _scope.BeginLifetimeScope(builder =>
            {
                builder.AddFileSystemEventClient();
                builder.RegisterInstance(new FileSystemConfig(connectionString))
                    .AsImplementedInterfaces().SingleInstance();
            });
            client = scope.Resolve<IEventClient>();
            return scope;
        }

        internal sealed class FileSystemConfig : IConfigureOptions<FileSystemEventClientOptions>
        {
            public FileSystemConfig(string outputFolder)
            {
                _outputFolder = outputFolder;
            }

            public void Configure(FileSystemEventClientOptions options)
            {
                options.OutputFolder = _outputFolder;
            }

            private readonly string _outputFolder;
        }

        private readonly ILifetimeScope _scope;
    }
}
