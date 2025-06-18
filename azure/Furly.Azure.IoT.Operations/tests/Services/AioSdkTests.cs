// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.StateStore;
    using global::Azure.Iot.Operations.Protocol; // For ApplicationContext
    using Moq;
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class AioSdkTests
    {
        [Fact]
        public async Task CreateAdrClientWrapperReturnsWrapperAsync()
        {
            Environment.SetEnvironmentVariable("ADR_RESOURCES_NAME_MOUNT_PATH", "test");
            var client = new Mock<IMqttPubSubClient>();
            client.SetupGet(x => x.ClientId).Returns("TestId").Verifiable(Times.AtLeast(1));
#pragma warning disable CA2000 // Dispose objects before losing scope
            var context = new ApplicationContext();
#pragma warning restore CA2000 // Dispose objects before losing scope
            using (var sdk = new AioSdk(context))
            {
                await using var result = sdk.CreateAdrClientWrapper(client.Object);
                Assert.NotNull(result);
            }
            Assert.True(IsDisposed(context));
            client.Verify();
        }

        [Fact]
        public async Task CreateAdrServiceClientReturnsServiceClientAsync()
        {
            var client = new Mock<IMqttPubSubClient>();
            client.SetupGet(x => x.ClientId).Returns("TestId").Verifiable(Times.AtLeast(1));
#pragma warning disable CA2000 // Dispose objects before losing scope
            var context = new ApplicationContext();
#pragma warning restore CA2000 // Dispose objects before losing scope
            using (var sdk = new AioSdk(context))
            {
                await using var result = sdk.CreateAdrServiceClient(client.Object);
                Assert.NotNull(result);
            }
            Assert.True(IsDisposed(context));
            client.Verify();
        }

        [Fact]
        public async Task CreateStateStoreClientReturnsStateStoreClientAsync()
        {
            var client = new Mock<IMqttPubSubClient>();
            client.SetupGet(x => x.ClientId).Returns("TestId").Verifiable(Times.AtLeast(1));
#pragma warning disable CA2000 // Dispose objects before losing scope
            var context = new ApplicationContext();
#pragma warning restore CA2000 // Dispose objects before losing scope
            using (var sdk = new AioSdk(context))
            {
                await using var result = sdk.CreateStateStoreClient(client.Object);
                Assert.NotNull(result);
            }
            Assert.True(IsDisposed(context));
            client.Verify();
        }

        [Fact]
        public async Task CreateSchemaRegistryClientReturnsSchemaRegistryClientAsync()
        {
            var client = new Mock<IMqttPubSubClient>();
            client.SetupGet(x => x.ClientId).Returns("TestId").Verifiable(Times.AtLeast(1));
#pragma warning disable CA2000 // Dispose objects before losing scope
            var context = new ApplicationContext();
#pragma warning restore CA2000 // Dispose objects before losing scope
            using (var sdk = new AioSdk(context))
            {
                await using var result = sdk.CreateSchemaRegistryClient(client.Object);
                Assert.NotNull(result);
            }
            Assert.True(IsDisposed(context));
            client.Verify();
        }

        [Fact]
        public void DisposeCallsDisposeAsyncOnContext()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var context = new ApplicationContext();
#pragma warning restore CA2000 // Dispose objects before losing scope
            var sdk = new AioSdk(context);
            Assert.False(IsDisposed(context));
            sdk.Dispose();
            Assert.True(IsDisposed(context));
        }

        private static bool IsDisposed(ApplicationContext context)
        {
            return (bool)typeof(ApplicationContext).GetField("_disposed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(context);
        }
    }
}
