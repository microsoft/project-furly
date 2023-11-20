// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Protocol.Tests
{
    using Furly.Tunnel.Protocol;
    using Furly.Extensions.Logging;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using AutoFixture;
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class ChunkMethodServerTests
    {
        [Theory, CombinatorialData]
        public async Task SendReceiveJsonTestWithVariousChunkSizesAsync(
            [CombinatorialValues(1024 * 1024, 120 * 1024, 100000, 20, 13, 1, 0)] int chunkSize)
        {
            var fixture = new Fixture();

            var expectedTarget = fixture.Create<string>();
            var expectedMethod = fixture.Create<string>();
            var expectedContentType = fixture.Create<string>();
            var expectedRequest = _serializer.SerializeToString(new
            {
                test1 = fixture.Create<string>(),
                test2 = fixture.Create<long>()
            });
            var expectedResponse = _serializer.SerializeToString(new
            {
                test1 = fixture.Create<byte[]>(),
                test2 = fixture.Create<string>()
            });
            using var methods = new ChunkMethodServer(_serializer,
                Log.Console<ChunkMethodServer>(), TimeSpan.FromSeconds(10))
            {
                Delegate = new FuncDelegate(string.Empty, (method, buffer, type, _) =>
                {
                    Assert.Equal(expectedMethod, method);
                    Assert.Equal(expectedContentType, type);
                    Assert.Equal(expectedRequest, Encoding.UTF8.GetString(buffer));
                    return Encoding.UTF8.GetBytes(expectedResponse);
                })
            };
            var server = new TestRpcServer(_serializer, chunkSize);
            var connection = await server.ConnectAsync(methods, default);
            try
            {
                var result = await server.CreateClient().CallMethodAsync(
                    expectedTarget, expectedMethod,
                    Encoding.UTF8.GetBytes(expectedRequest), expectedContentType);
                Assert.Equal(expectedResponse, Encoding.UTF8.GetString(result.Span));
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        [Theory, CombinatorialData]
        public async Task SendReceiveLargeBufferTestWithVariousChunkSizesAsync(
            [CombinatorialValues(455585, 300000, 233433, 200000, 100000, 120 * 1024, 99, 13, 20, 0)] int chunkSize)
        {
            var fixture = new Fixture();

            var expectedTarget = fixture.Create<string>();
            var expectedMethod = fixture.Create<string>();
            var expectedContentType = fixture.Create<string>();

#pragma warning disable CA5394 // Do not use insecure randomness
            var expectedRequest = new byte[200000];
            kRand.NextBytes(expectedRequest);
            var expectedResponse = new byte[300000];
            kRand.NextBytes(expectedResponse);
#pragma warning restore CA5394 // Do not use insecure randomness

            using var methods = new ChunkMethodServer(_serializer,
                Log.Console<ChunkMethodServer>(), TimeSpan.FromSeconds(10))
            {
                Delegate = new FuncDelegate(string.Empty, (method, buffer, type, _) =>
                {
                    Assert.Equal(expectedMethod, method);
                    Assert.Equal(expectedContentType, type);
                    Assert.Equal(expectedRequest, buffer);
                    return expectedResponse;
                })
            };
            var server = new TestRpcServer(_serializer, chunkSize);
            var connection = await server.ConnectAsync(methods, default);
            try
            {
                var result = await server.CreateClient().CallMethodAsync(
                    expectedTarget, expectedMethod,
                    expectedRequest, expectedContentType);
                Assert.True(result.Span.SequenceEqual(expectedResponse));
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        private static readonly Random kRand = new();
        private readonly IJsonSerializer _serializer = new DefaultJsonSerializer();
    }
}
