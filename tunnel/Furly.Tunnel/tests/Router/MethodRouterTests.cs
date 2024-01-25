// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using Furly.Tunnel.Router.Services;
    using Furly.Tunnel.Protocol;
    using Furly.Exceptions;
    using Furly.Extensions.Logging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using Autofac;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class MethodRouterTests
    {
        private readonly DefaultJsonSerializer _serializer = new();
        private readonly ITestOutputHelper _output;

        public MethodRouterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Test1InvocationNonChunkedAsync()
        {
            await using var router = GetRouter(out _);

            var buffer = new byte[1049];
            FillRandom(buffer);
            var expected = new TestModel { Test = buffer };
            var response = await router.InvokeAsync("Test1_V1",
                _serializer.SerializeObjectToMemory(expected),
                ContentMimeType.Json, default);

            var returned = _serializer.Deserialize<TestModel>(response);
            Assert.Equal(expected.Test, returned!.Test);
        }

        [Fact]
        public async Task Test1InvocationChunkedAsync()
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());

            var buffer = new byte[300809];
            FillRandom(buffer);
            var expected = new TestModel { Test = buffer };
            var response = await client.CallMethodAsync("test", "Test1_V1",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<TestModel>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected.Test, returned!.Test);
        }

        [Fact]
        public async Task Test1InvocationNonChunkedWithCancellationAsync()
        {
            await using var router = GetRouter(out _);

            var buffer = new byte[1049];
            FillRandom(buffer);
            var expected = new TestModel { Test = buffer };
            var response = await router.InvokeAsync("Test1C_V1",
                _serializer.SerializeObjectToMemory(expected),
                ContentMimeType.Json, default);

            var returned = _serializer.Deserialize<TestModel>(response);
            Assert.Equal(expected.Test, returned!.Test);
        }

        [Fact]
        public async Task Test1InvocationChunkedWithCancellationAsync()
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());

            var buffer = new byte[300809];
            FillRandom(buffer);
            var expected = new TestModel { Test = buffer };
            var response = await client.CallMethodAsync("test", "Test1C_V1",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<TestModel>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected.Test, returned!.Test);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(64 * 1024)]
        [InlineData(95 * 1024)]
        public async Task Test2InvocationNonChunkedAsync(int size)
        {
            await using var router = GetRouter(out _);
            var expected = new byte[size];
            FillRandom(expected);
            var response = await router.InvokeAsync(
                "Test2_V1", _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, default);
            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(64 * 1024)]
        [InlineData(95 * 1024)]
        public async Task Test2InvocationNonChunkedWithCancellationAsync(int size)
        {
            await using var router = GetRouter(out _);
            var expected = new byte[size];
            FillRandom(expected);
            var response = await router.InvokeAsync(
                "Test2C_V1", _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, default);
            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(64 * 1024)]
        [InlineData(95 * 1024)]
        public async Task Test8InvocationV1NonChunkedAsync(int size)
        {
            await using var router = GetRouter(out _);
            var expected = new byte[size];
            FillRandom(expected);
            var response = await router.InvokeAsync(
                "Test8_V1", _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, default);
            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(64 * 1024)]
        [InlineData(95 * 1024)]
        public async Task Test8InvocationV2NonChunkedAsync(int size)
        {
            await using var router = GetRouter(out _);
            var expected = new byte[size];
            FillRandom(expected);
            var response = await router.InvokeAsync(
                "Test8_V2", _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, default);
            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(128 * 1024)]
        [InlineData(450000)]
        [InlineData(129 * 1024)]
        public async Task Test2InvocationChunkedAsync(int size)
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());
            var expected = new byte[size];
            FillRandom(expected);
            var response = await client.CallMethodAsync("test", "Test2_V1",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<byte[]>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(128 * 1024)]
        [InlineData(450000)]
        [InlineData(129 * 1024)]
        public async Task Test2InvocationChunkedWithCancellationAsync(int size)
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());
            var expected = new byte[size];
            FillRandom(expected);
            var response = await client.CallMethodAsync("test", "Test2C_V1",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<byte[]>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(128 * 1024)]
        [InlineData(450000)]
        [InlineData(129 * 1024)]
        public async Task Test8InvocationV1ChunkedAsync(int size)
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());
            var expected = new byte[size];
            FillRandom(expected);
            var response = await client.CallMethodAsync("test", "Test8_V1",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<byte[]>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected, returned);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19)]
        [InlineData(1049)]
        [InlineData(128 * 1024)]
        [InlineData(450000)]
        [InlineData(129 * 1024)]
        public async Task Test8InvocationV2ChunkedAsync(int size)
        {
            await using var router = GetRouter(out var rpcClient);
            var client = new ChunkMethodClient(rpcClient,
                _serializer, Log.Console<ChunkMethodClient>());
            var expected = new byte[size];
            FillRandom(expected);
            var response = await client.CallMethodAsync("test", "Test8_V2",
                _serializer.SerializeObjectToMemory(expected),
                    ContentMimeType.Json, null, default);

            var returned = _serializer.Deserialize<byte[]>(
                Encoding.UTF8.GetString(response.Span));
            Assert.Equal(expected, returned);
        }

        [Fact]
        public async Task Test3InvocationNonChunkedAsync()
        {
            await using var router = GetRouter(out _);
            var expected = new byte[1049];
            FillRandom(expected);
            var response = await router.InvokeAsync("Test3_V1",
                _serializer.SerializeObjectToMemory(new
                {
                    request = expected,
                    Value = 3254
                }), ContentMimeType.Json, default);

            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Fact]
        public async Task Test3InvocationNonChunkedWithCancellationAsync()
        {
            await using var router = GetRouter(out _);
            var expected = new byte[1049];
            FillRandom(expected);
            var response = await router.InvokeAsync("Test3C_V1",
                _serializer.SerializeObjectToMemory(new
                {
                    request = expected,
                    Value = 3254
                }), ContentMimeType.Json, default);

            var returned = _serializer.Deserialize<byte[]>(response);
            Assert.Equal(expected, returned);
        }

        [Fact]
        public async Task Test2InvocationV2NonChunkedAsync()
        {
            await using var router = GetRouter(out _);
            var buffer = new byte[1049];
            FillRandom(buffer);
            try
            {
                var response = await router.InvokeAsync(
                    "Test2_v2", _serializer.SerializeObjectToMemory(buffer),
                    ContentMimeType.Json, default);
            }
            catch (MethodCallStatusException m)
            {
                Assert.Equal(400, m.Result);
                var deserializedResponse = _serializer.Deserialize<TestExceptionModel>(
                    m.ResponsePayload);
                var ex = deserializedResponse!.Details!.ConvertTo<ArgumentNullException>();
                Assert.NotNull(ex);
                return;
            }
            Assert.False(true);
        }

        [Fact]
        public async Task Test3InvocationV2NonChunkedAsync()
        {
            await using var router = GetRouter(out _);
            var buffer = new byte[1049];
            FillRandom(buffer);
            const int expected = 3254;
            var response = await router.InvokeAsync("Test3_v2",
                _serializer.SerializeObjectToMemory(new
                {
                    request = buffer,
                    Value = expected
                }), ContentMimeType.Json, default);

            var returned = _serializer.Deserialize<int>(response);
            Assert.Equal(expected, returned);
        }

        internal static void FillRandom(byte[] expected)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            Random.Shared.NextBytes(expected);
#pragma warning restore CA5394 // Do not use insecure randomness
        }

        internal MethodRouter GetRouter(out IRpcClient client, int size = 128 * 1024)
        {
            var server = new TestRpcServer(_serializer, size);
            client = server;
            return new MethodRouter(server.YieldReturn(), _serializer,
                _output.ToLogger<MethodRouter>())
            {
                Controllers = GetControllers()
            };
        }

        /// <summary>
        /// Create container
        /// </summary>
        /// <param name="controllers"></param>
        /// <returns></returns>
        internal static async Task<IContainer> CreateContainerAsync(
            IEnumerable<IMethodController>? controllers = null)
        {
            var builder = new ContainerBuilder();
            builder.AddLogging();

            foreach (var controller in controllers ?? GetControllers())
            {
                builder.RegisterInstance(controller)
                    .As<IMethodController>();
            }

            builder.RegisterType<MethodRouter>()
                .AsImplementedInterfaces().SingleInstance()
                .PropertiesAutowired(
                    PropertyWiringOptions.AllowCircularDependencies);

            builder.RegisterType<TestRpcServer>() // Exposes IRpcClient
                .AsImplementedInterfaces().SingleInstance();
            builder.AddDefaultJsonSerializer();

            builder.RegisterType<ChunkMethodClient>()
                .AsImplementedInterfaces().SingleInstance();

            var container = builder.Build();
            await container.Resolve<IEnumerable<IAwaitable>>()
                .WhenAll().ConfigureAwait(false);
            return container;
        }

        internal static List<IMethodController> GetControllers()
        {
            return new List<IMethodController> {
                new TestControllerV1(),
                new TestControllerV1WithCancellationToken(),
                new TestControllerV2(),
                new TestControllerV1And2()
            };
        }
    }
}
