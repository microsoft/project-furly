// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Services
{
    using Furly.Tunnel.AspNetCore.Tests;
    using Furly.Tunnel.AspNetCore.Tests.Server.Models;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class HttpTunnelMethodClientSimpleTests : IClassFixture<InMemoryServerFixture>
    {
        private readonly InMemoryServerFixture _fixture;

        public HttpTunnelMethodClientSimpleTests(InMemoryServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TestInvokeSimple1Async()
        {
            var server = _fixture.Resolve<IRpcServer>().Connected.First();
            var serializer = _fixture.Resolve<IJsonSerializer>();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };
            var result = await server.InvokeAsync("v2/path/test/1245",
                serializer.SerializeToMemory(expected).ToArray(),
                null!);

            var response = serializer.Deserialize<TestResponseModel>(result);

            Assert.Equal(expected.Input, response?.Input);
            Assert.Equal("Post", response?.Method);
            Assert.Equal("1245", response?.Id);
        }

        [Fact]
        public async Task TestInvokeSimple2Async()
        {
            var server = _fixture.Resolve<IRpcServer>().Connected.First();
            var serializer = _fixture.Resolve<IJsonSerializer>();

            var expected = new TestRequestModel
            {
                Input = null
            };
            var result = await server.InvokeAsync("v2/path/test/hahahaha",
                serializer.SerializeToMemory(expected).ToArray(),
                null!);

            var response = serializer.Deserialize<TestResponseModel>(result);

            Assert.Equal(expected.Input, response?.Input);
            Assert.Equal("Post", response?.Method);
            Assert.Equal("hahahaha", response?.Id);
        }

        [Fact]
        public async Task TestInvokeSimpleWithBadArgThrowsAsync()
        {
            var server = _fixture.Resolve<IRpcServer>().Connected.First();
            var serializer = _fixture.Resolve<IJsonSerializer>();
            var expected = new TestRequestModel
            {
                Input = "test"
            };

            await Assert.ThrowsAsync<MethodCallStatusException>(
                () => server.InvokeAsync("v2/path/test",
                serializer.SerializeToMemory(expected).ToArray(),
                    null!).AsTask());

            await Assert.ThrowsAsync<MethodCallStatusException>(
                () => server.InvokeAsync("v2/path/test", null!,
                    null!).AsTask());
        }
    }
}
