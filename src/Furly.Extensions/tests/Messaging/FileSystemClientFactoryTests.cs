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
    using System.IO;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public sealed class FileSystemClientFactoryTests : IDisposable
    {
        public FileSystemClientFactoryTests()
        {
            var builder = new ContainerBuilder();

            // Register necessary dependencies for FileSystemEventClient
            builder.RegisterType<FileSystemEventClient>()
                .As<IEventClient>()
                .InstancePerLifetimeScope();
            builder.AddLogging();

            builder.RegisterInstance(Options.Create(new FileSystemEventClientOptions
            {
                OutputFolder = ".",
                MessageMaxBytes = 1024 * 1024
            })).As<IOptions<FileSystemEventClientOptions>>();

            _scope = builder.Build();
        }

        [Fact]
        public void FileSystemClientFactoryHasCorrectName()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);

            // Act & Assert
            Assert.Equal("FileSystem", factory.Name);
        }

        [Fact]
        public void CreateEventClientWithValidParametersCreatesClient()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var connectionString = Path.GetTempFileName();
            using var f = new DeferCleanup(connectionString);

            // Act
            using var disposable = factory.CreateEventClient(connectionString, out var client);

            // Assert
            Assert.NotNull(client);
            Assert.NotNull(disposable);
            Assert.Equal("FileSystem", client.Name);
        }

        [Fact]
        public void CreateEventClientSamePathReturnsSharedClient()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var connectionString = Path.GetTempFileName();
            using var f = new DeferCleanup(connectionString);

            // Act - Create first client
            using var disposable1 = factory.CreateEventClient(connectionString, out var client1);

            // Act - Create second client with same path (connectionString + context)
            using var disposable2 = factory.CreateEventClient(connectionString, out var client2);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.Same(client1, client2); // Should be the same instance (shared)
        }

        [Fact]
        public void CreateEventClientDifferentPathsReturnsDifferentClients()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var connectionString1 = Path.GetTempFileName();
            var connectionString2 = Path.GetTempFileName();
            using var f1 = new DeferCleanup(connectionString1);
            using var f2 = new DeferCleanup(connectionString2);

            // Act
            using var disposable1 = factory.CreateEventClient(connectionString1, out var client1);
            using var disposable2 = factory.CreateEventClient(connectionString2, out var client2);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            // Should be different instances because the path is different
            Assert.NotSame(client1, client2);
        }

        [Fact]
        public void CreateEventClientDifferentConnectionStringsDifferentClients()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var connectionString1 = Path.Combine(Path.GetTempPath(), "folder1");
            var connectionString2 = Path.Combine(Path.GetTempPath(), "folder2");
            using var f1 = new DeferCleanup(connectionString1);
            using var f2 = new DeferCleanup(connectionString2);

            // Act
            using var disposable1 = factory.CreateEventClient(connectionString1, out var client1);
            using var disposable2 = factory.CreateEventClient(connectionString2, out var client2);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotSame(client1, client2); // Should be different instances
        }

        [Fact]
        public void CreateEventClientNormalizedPathsReturnSameClient()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            const string relativePath = "temp";
            var absolutePath = Path.GetFullPath(relativePath);
            using var f = new DeferCleanup(absolutePath);


            // Act
            using var disposable1 = factory.CreateEventClient(relativePath, out var client1);
            using var disposable2 = factory.CreateEventClient(absolutePath, out var client2);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            // Should be the same instance because Path.GetFullPath normalizes both to the same path
            Assert.Same(client1, client2);
        }

        [Fact]
        public void CreateEventClientCaseInsensitivePaths()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var basePath = Path.GetTempFileName();
            using var f = new DeferCleanup(basePath);

            // Test case insensitivity of the dictionary lookup
            var path1 = basePath;
            var path2 = basePath.ToUpperInvariant(); // Same path for case-insensitive test

            // Act
            using var disposable1 = factory.CreateEventClient(path1, out var client1);
            using var disposable2 = factory.CreateEventClient(path2, out var client2);

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.Same(client1, client2); // Should be the same because dictionary is case-insensitive
        }

        [Fact]
        public void CreateEventClientReferenceCountingWorksCorrectly()
        {
            // Arrange
            using var factory = new FileSystemClientFactory(_scope);
            var connectionString = Path.GetTempFileName();
            using var f = new DeferCleanup(connectionString);

            // Act - Create multiple references to the same client
            var disposable1 = factory.CreateEventClient(connectionString, out var client1);
            var disposable2 = factory.CreateEventClient(connectionString, out var client2);
            var disposable3 = factory.CreateEventClient(connectionString, out var client3);

            // Assert - All should be the same instance
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotNull(client3);
            Assert.Same(client1, client2);
            Assert.Same(client1, client3);

            // Dispose one reference - others should still work
            disposable1.Dispose();

            // Client should still work since other references exist
            var event1 = client2.CreateEvent();
            Assert.NotNull(event1);
            event1.Dispose();

            // Create another reference - should still get the same client
            var disposable4 = factory.CreateEventClient(connectionString, out var client4);
            Assert.Same(client1, client4);

            // Dispose more references
            disposable2.Dispose();
            disposable3.Dispose();

            // Client should still work since we have disposable4
            var event2 = client4.CreateEvent();
            Assert.NotNull(event2);
            event2.Dispose();

            // Dispose the last reference
            disposable4.Dispose();

            // Now create a new client - should get a new instance since the previous scope was cleaned up
            using var disposable5 = factory.CreateEventClient(connectionString, out var client5);
            Assert.NotNull(client5);
            // Note: We can't assert NotSame here because the underlying Autofac container might reuse instances
        }

        [Fact]
        public void DisposeDisposesUnderlyingScope()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<FileSystemEventClient>()
                .As<IEventClient>()
                .InstancePerLifetimeScope();
            builder.AddLogging();
            builder.RegisterInstance(Options.Create(new FileSystemEventClientOptions()))
                .As<IOptions<FileSystemEventClientOptions>>();

            var scope = builder.Build();
            var factory = new FileSystemClientFactory(scope);

            // Act
            factory.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(scope.Resolve<IEventClient>);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }

        internal sealed record class DeferCleanup(string Path) : IDisposable
        {
            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, true);
                }
                catch
                {
                }
            }
        }

        private readonly ILifetimeScope _scope;
    }
}
