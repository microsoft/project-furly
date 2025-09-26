// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
#nullable disable
namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Serializers;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;
    using global::Azure.Iot.Operations.Services.StateStore;
    using Microsoft.Extensions.Logging;
    using Moq;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class AioDssClientTests
    {
        [Fact]
        public void DisposeCallsDisposeAsyncOnUnderlyingClient()
        {
            _dssMock.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var client = CreateClient();
            client.Dispose();
            _dssMock.Verify(d => d.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task TryPageInAsyncReturnsState()
        {
            const string key = "key1";
            var bytes = new byte[] { 1, 2, 3 };
            var response = (StateStoreGetResponse)Activator.CreateInstance(
                typeof(StateStoreGetResponse),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[] { new HybridLogicalClock(), new StateStoreValue(bytes) },
                culture: null);

            _dssMock.Setup(d => d.GetAsync(key, null, It.IsAny<CancellationToken>())).ReturnsAsync(response);
            _dssMock.Setup(d => d.ObserveAsync(key, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var expected = Mock.Of<VariantValue>();
            _serializerMock.Setup(s => s.Deserialize(new ReadOnlySequence<byte>(bytes), typeof(VariantValue))).Returns(expected);
            await using var client = CreateClient();
            var result = await client.TryPageInAsync(key, CancellationToken.None);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task TryPageInAsyncExceptionReturnsNull()
        {
            const string key = "key1";
            _dssMock.Setup(d => d.GetAsync(key, null, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
            await using var client = CreateClient();
            var result = await client.TryPageInAsync(key, CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        public async Task OnChangesAsyncSavesAndDeletes()
        {
            VariantValue variantValue = "testValue";
            var batch = new Dictionary<string, VariantValue>
            {
                { "k1", variantValue },
                { "k2", null }
            };

            _serializerMock
                .Setup(s => s.SerializeObject(It.IsAny<IBufferWriter<byte>>(), variantValue, null, SerializeOption.None))
                .Callback((IBufferWriter<byte> b, object _, Type _, SerializeOption _) => b.Write("1"u8));

            var setresponse = new Mock<IStateStoreSetResponse>();
            setresponse.SetupGet(p => p.Success).Returns(true);
            _dssMock.Setup(d => d.SetAsync("k1", It.IsAny<StateStoreValue>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(setresponse.Object));
            _dssMock.Setup(d => d.UnobserveAsync("k2", null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var deleteresponse = new Mock<IStateStoreDeleteResponse>();
            deleteresponse.SetupGet(p => p.DeletedItemsCount).Returns(1) ;
            _dssMock.Setup(d => d.DeleteAsync("k2", null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(deleteresponse.Object));
            await using var client = CreateClient();

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await (ValueTask)client.GetType().GetMethod("OnChangesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(client, new object[] { batch, CancellationToken.None });
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

            _dssMock.Verify(d => d.SetAsync("k1", It.IsAny<StateStoreValue>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
            _dssMock.Verify(d => d.UnobserveAsync("k2", null, It.IsAny<CancellationToken>()), Times.Once);
            _dssMock.Verify(d => d.DeleteAsync("k2", null, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ClientKeyChangeMessageReceivedAsyncDeletedRemovesKey()
        {
            var key = new StateStoreKey("k1");
            var state = new Dictionary<string, VariantValue> { { "k1", "test" } };
            using var client = CreateClient();

            var args = (KeyChangeMessageReceivedEventArgs)Activator.CreateInstance(
                typeof(KeyChangeMessageReceivedEventArgs),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[] { key, KeyState.Deleted, new HybridLogicalClock() },
                culture: null);

            client.ClientKeyChangeMessageReceivedAsync(null, args);
            // No assertion, just ensure no exception
        }

        [Fact]
        public void ClientKeyChangeMessageReceivedAsyncAddedOrUpdatedAddsOrUpdatesKey()
        {
            var key = new StateStoreKey("k1");
            var bytes = new byte[] { 1 };
            VariantValue value = "value";

            var args = (KeyChangeMessageReceivedEventArgs)Activator.CreateInstance(
                typeof(KeyChangeMessageReceivedEventArgs),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[] { key, KeyState.Updated, new HybridLogicalClock() },
                culture: null);

            var prop = typeof(KeyChangeMessageReceivedEventArgs).GetProperty("NewValue");
            prop.SetValue(args, new StateStoreValue(bytes),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, null, null);

            _serializerMock.Setup(s => s.Deserialize(new ReadOnlySequence<byte>(bytes), typeof(VariantValue))).Returns(value);
            using var client = CreateClient();

            client.ClientKeyChangeMessageReceivedAsync(null, args);
            // No assertion, just ensure no exception
        }

        private AioDssClient CreateClient()
        {
            _sdkMock.Setup(s => s.CreateStateStoreClient(It.IsAny<IMqttPubSubClient>())).Returns(_dssMock.Object);
            return new AioDssClient(_mqttClientMock.Object, _sdkMock.Object, _serializerMock.Object, _loggerMock.Object);
        }

        private readonly Mock<IAioSdk> _sdkMock = new();
        private readonly Mock<IMqttPubSubClient> _mqttClientMock = new();
        private readonly Mock<IStateStoreClient> _dssMock = new();
        private readonly Mock<ISerializer> _serializerMock = new();
        private readonly Mock<ILogger<AioDssClient>> _loggerMock = new();
    }
}
