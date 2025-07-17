// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
#nullable disable
namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Logging;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
    using Moq;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class AioSrClientTests
    {
        [Fact]
        public void DisposeCallsDisposeAsyncMethodOnClient()
        {
            _clientMock.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var client = new AioSrClient(_sdkMock.Object, _mqttClientMock.Object, Log.Console<AioSrClient>());
            client.Dispose();
            _clientMock.Verify(c => c.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterAsyncJsonSchemaSuccess()
        {
            var schema = new Mock<IEventSchema>();
            schema.SetupGet(s => s.Type).Returns(ContentMimeType.JsonSchema);
            schema.SetupGet(s => s.Schema).Returns("myschema");
            schema.SetupGet(s => s.Name).Returns("myname");
            var expected = new Schema
            {
                Name = "myname",
                Namespace = "ns"
            };
            _clientMock.Setup(c => c.PutAsync("myschema", Format.JsonSchemaDraft07, SchemaType.MessageSchema,
                 "1.0.0", null, null, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
            using var client = new AioSrClient(_sdkMock.Object, _mqttClientMock.Object, Log.Console<AioSrClient>());
            var result = await client.RegisterAsync(schema.Object, CancellationToken.None);
            Assert.Equal("myname", result);
        }

        [Fact]
        public async Task RegisterAsyncUnsupportedTypeThrows()
        {
            var schema = new Mock<IEventSchema>();
            schema.SetupGet(s => s.Type).Returns("unsupported");
            using var client = new AioSrClient(_sdkMock.Object, _mqttClientMock.Object, Log.Console<AioSrClient>());
            await Assert.ThrowsAsync<NotSupportedException>(
                async () => await client.RegisterAsync(schema.Object, CancellationToken.None).ConfigureAwait(false));
        }

        [Fact]
        public async Task RegisterAsyncNullResultThrows()
        {
            var schema = new Mock<IEventSchema>();
            schema.SetupGet(s => s.Type).Returns(ContentMimeType.JsonSchema);
            schema.SetupGet(s => s.Schema).Returns("myschema");
            schema.SetupGet(s => s.Name).Returns("myname");
            _clientMock.Setup(c => c.PutAsync("myschema", Format.JsonSchemaDraft07, SchemaType.MessageSchema,
                 "1.0.0", null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<Schema>(null));
            using var client = new AioSrClient(_sdkMock.Object, _mqttClientMock.Object, Log.Console<AioSrClient>());
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.RegisterAsync(schema.Object, CancellationToken.None).ConfigureAwait(false));
        }

        public AioSrClientTests()
        {
            _sdkMock.Setup(s => s.CreateSchemaRegistryClient(It.IsAny<IMqttPubSubClient>()))
                .Returns(_clientMock.Object);
        }

        private readonly Mock<IAioSdk> _sdkMock = new();
        private readonly Mock<IMqttPubSubClient> _mqttClientMock = new();
        private readonly Mock<ISchemaRegistryClient> _clientMock = new();
    }
}
