// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using Google.Protobuf;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [IntegrationTest]
    public sealed class DaprKeyValueStoreTests : IDisposable
    {
        private readonly DaprClientHarness _harness;

        public DaprKeyValueStoreTests(ITestOutputHelper output)
        {
            _harness = new DaprClientHarness(output);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [Fact]
        public async Task RetrieveNonExistantObject1Async()
        {
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            Assert.Throws<KeyNotFoundException>(() => keyValueStore.State["Test"]);
        }

        [Fact]
        public async Task RetrieveNonExistantObject2Async()
        {
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            Assert.False(keyValueStore.State.TryGetValue("Test", out _));
        }

        [Fact]
        public async Task RetrieveNonExistantObject4Async()
        {
            var keyValueStore = await _harness.GetKeyValueStoreAsync();

            var result = await keyValueStore.TryPageInAsync("test");
            Assert.Null(result);
        }

        [Fact]
        public async Task RetrieveExistingObject1Async()
        {
            var serializer = new DefaultJsonSerializer();
            _harness.GetSidecarStorage().Items.TryAdd("test",
                ByteString.CopyFrom(serializer.SerializeToMemory(10).ToArray()));

            var keyValueStore = await _harness.GetKeyValueStoreAsync();

            Assert.True(keyValueStore.State.TryGetValue("test", out _));

            var result = await keyValueStore.TryPageInAsync("test");
            Assert.NotNull(result);
            Assert.False(result!.IsNull);
            Assert.Equal(10, (int)result!);
        }

        [Fact]
        public async Task RetrieveExistingObject2Async()
        {
            var serializer = new DefaultJsonSerializer();
            var storage = _harness.GetSidecarStorage();
            storage.HasNoQuerySupport = true;
            storage.Items.TryAdd("test",
                ByteString.CopyFrom(serializer.SerializeToMemory(10).ToArray()));

            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            Assert.False(keyValueStore.State.TryGetValue("test", out _));

            var result = await keyValueStore.TryPageInAsync("test");
            Assert.NotNull(result);
            Assert.False(result!.IsNull);
            Assert.Equal(10, (int)result!);
        }

        [Fact]
        public async Task StoreAndRetrieveIntTestAsync()
        {
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            keyValueStore.State["test"] = 10;

            await _harness.GetSidecarStorage().WaitUntil("test");

            var result = await keyValueStore.TryPageInAsync("test");
            Assert.NotNull(result);
            Assert.Equal(10, (int)result!);
        }

        [Fact]
        public async Task StoreRemoveAndRetrieveIntTestAsync()
        {
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            keyValueStore.State["test"] = 10;
            await _harness.GetSidecarStorage().WaitUntil("test");

            keyValueStore.State.Remove("test");
            await _harness.GetSidecarStorage().WaitUntil("test", false);

            var result = await keyValueStore.TryPageInAsync("test");
            Assert.Null(result);
        }

        [Fact]
        public async Task StoreAndRetrieveObject1TestAsync()
        {
            var serializer = new DefaultJsonSerializer();
            var expected = new
            {
                test = 10,
                test2 = "string"
            };
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            keyValueStore.State["test2"] = serializer.FromObject(expected);
            await _harness.GetSidecarStorage().WaitUntil("test2");

            var result = await keyValueStore.TryPageInAsync("test2");
            Assert.NotNull(result);
            Assert.True(result!.TryGetProperty("test", out var intValue));
            Assert.Equal(10, (int)intValue!);
            Assert.True(result!.TryGetProperty("test2", out var strValue));
            Assert.Equal("string", (string)strValue!);
        }

        [Fact]
        public async Task StoreAndRetrieveObject2TestAsync()
        {
            var serializer = new DefaultJsonSerializer();
            var expected = new State
            {
                Off = true,
                Id = Guid.NewGuid(),
                Status = Status.NotRunning
            };
            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            keyValueStore.State["state"] = serializer.FromObject(expected);
            await _harness.GetSidecarStorage().WaitUntil("state");

            var item = await keyValueStore.TryPageInAsync("state");
            Assert.NotNull(item);
            var result = item!.ConvertTo<State>()!;

            Assert.NotNull(result);
            Assert.Equal(expected.Off, result.Off);
            Assert.Equal(expected.Id, result.Id);
            Assert.Equal(expected.Status, result.Status);
        }

        [Fact]
        public async Task StoreAndRetrieveObject3TestAsync()
        {
            var serializer = new DefaultJsonSerializer();
            var expected = new State
            {
                Off = true,
                Id = Guid.NewGuid(),
                Status = Status.NotRunning
            };
            _harness.GetSidecarStorage().Items.TryAdd("state",
                ByteString.CopyFrom(serializer.SerializeToMemory(expected).ToArray()));

            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            Assert.True(keyValueStore.State.TryGetValue("state", out var item));
            Assert.NotNull(item);
            var result = item!.ConvertTo<State>()!;

            Assert.NotNull(result);
            Assert.Equal(expected.Off, result.Off);
            Assert.Equal(expected.Id, result.Id);
            Assert.Equal(expected.Status, result.Status);
        }

        [Fact]
        public async Task StoreAndRetrieveObject4TestAsync()
        {
            var serializer = new DefaultJsonSerializer();
            var expected = new State
            {
                Off = true,
                Id = Guid.NewGuid(),
                Status = Status.NotRunning
            };
            var storage = _harness.GetSidecarStorage();
            storage.HasNoQuerySupport = true;
            storage.Items.TryAdd("state",
                ByteString.CopyFrom(serializer.SerializeToMemory(expected).ToArray()));

            var keyValueStore = await _harness.GetKeyValueStoreAsync();
            Assert.False(keyValueStore.State.TryGetValue("state", out var item));
            Assert.Null(item);

            item = await keyValueStore.TryPageInAsync("state");
            Assert.NotNull(item);
            var result = item!.ConvertTo<State>()!;

            Assert.NotNull(result);
            Assert.Equal(expected.Off, result.Off);
            Assert.Equal(expected.Id, result.Id);
            Assert.Equal(expected.Status, result.Status);
        }
    }

    /// <summary>
    /// State
    /// </summary>
    [DataContract]
    public sealed class State
    {
        [DataMember]
        public bool Off { get; set; }
        [DataMember]
        public Guid Id { get; set; }
        [DataMember]
        public Status Status { get; set; }
    }

    /// <summary>
    /// Enum
    /// </summary>
    [DataContract]
    public enum Status
    {
        [EnumMember]
        Running = 0,

        [EnumMember]
        NotRunning = 1,
    }
}
