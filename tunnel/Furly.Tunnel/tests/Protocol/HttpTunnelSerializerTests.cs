// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Protocol.Tests
{
    using Furly.Tunnel.Protocol;
    using Furly.Tunnel.Models;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using AutoFixture;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security",
        "CA5394:Do not use insecure randomness", Justification = "Tests")]
    public class HttpTunnelSerializerTests
    {
        [Theory, CombinatorialData]
        public void SerializeRequestTest(
            [CombinatorialValues(0, 1, 256, 256 * 1024, 1024 * 1024)] int payloadSize,
            [CombinatorialValues(120 * 1024, 256 * 1024, 1024 * 1024)] int maxBufferSize)
        {
            var fixture = new Fixture();
            var request = fixture.Build<HttpTunnelRequestModel>()
                .Without(f => f.Body)
                .Create();
            var original = new byte[payloadSize];
            request.Body = original;
            kRand.NextBytes(request.Body);

            var buffers = _serializer.SerializeRequest(request, maxBufferSize);
            Assert.All(buffers, buffer => Assert.True(buffer.Length <= maxBufferSize));

            var chunk0 = _serializer.DeserializeRequest0(buffers[0],
                out var request2, out var chunks);
            Assert.NotNull(chunk0);
            Assert.Null(request2.Body);
            Assert.Equal(buffers.Count, chunks + 1);
            Assert.Equal(request.Method, request2.Method);
            Assert.Equal(request.Uri, request2.Uri);
            Assert.Equal(request.RequestId, request2.RequestId);
            Assert.Equal(request.RequestHeaders, request2.RequestHeaders,
                Compare.Using<Dictionary<string, List<string>>?>(HeaderEquals));
            Assert.Equal(request.ContentHeaders, request2.ContentHeaders,
                Compare.Using<Dictionary<string, List<string>>?>(HeaderEquals));

            var original2 = chunk0.Unpack(buffers);
            Assert.Equal(original, original2);
        }

        [Theory, CombinatorialData]
        public void SerializeResponseTest(
            [CombinatorialValues(0, 1, 256, 256 * 1024, 1024 * 1024)] int payloadSize,
            [CombinatorialValues(120 * 1024, 256 * 1024, 1024 * 1024)] int maxBufferSize)
        {
            var fixture = new Fixture();
            var response = fixture.Build<HttpTunnelResponseModel>()
                .Without(f => f.Payload)
                .Create();
            var original = new byte[payloadSize];
            kRand.NextBytes(original);
            response.Payload = original;

            var buffers = _serializer.SerializeResponse(response, maxBufferSize);
            Assert.All(buffers, buffer => Assert.True(buffer.Length <= maxBufferSize));

            var chunk0 = _serializer.DeserializeResponse0(buffers[0],
                out var request2, out var chunks);
            Assert.NotNull(chunk0);
            Assert.Null(request2.Payload);
            Assert.Equal(buffers.Count, chunks + 1);
            Assert.Equal(response.Status, request2.Status);
            Assert.Equal(response.RequestId, request2.RequestId);
            Assert.Equal(response.Headers, request2.Headers,
                Compare.Using<Dictionary<string, List<string>>?>(HeaderEquals));

            var original2 = chunk0.Unpack(buffers);
            Assert.Equal(original, original2);
        }

        private static bool HeaderEquals(Dictionary<string, List<string>>? t1,
            Dictionary<string, List<string>>? t2)
        {
            if (t1 == null || t2 == null)
            {
                return t1 == t2;
            }

            if (!t1.Keys.SequenceEqual(t2.Keys))
            {
                return false;
            }
            var values1 = t1.Values.ToList();
            var values2 = t2.Values.ToList();
            for (var index = 0; index < values1.Count; index++)
            {
                if (!values1[index].SequenceEqual(values2[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static readonly Random kRand = new();
        private readonly IJsonSerializer _serializer = new DefaultJsonSerializer();
    }
}
