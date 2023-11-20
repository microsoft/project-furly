// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Services
{
    using Furly.Tunnel.AspNetCore.Tests;
    using Furly.Tunnel.AspNetCore.Tests.Server.Models;
    using Furly.Extensions.Serializers;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class HttpTunnelEventClientTests : IClassFixture<InMemoryServerFixture>
    {
        private readonly InMemoryServerFixture _fixture;

        public HttpTunnelEventClientTests(InMemoryServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestGetRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            // Perform get
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(httpRequest);
            var httpResponse = await client.SendAsync(httpRequest);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);

            Assert.Null(response.Input);
            Assert.Equal("Get", response.Method);
            Assert.Equal("eventClientTestId", response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPutRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform put
            var uri = new UriBuilder
            {
                Path = "v2/path/test"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Put", response.Method);
            Assert.NotNull(response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPutAndGetRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform put
            var uri = new UriBuilder
            {
                Path = "v2/path/test"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);

            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);
            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Put", response.Method);
            Assert.NotNull(response.Id);

            var id = response.Id;
            uri = new UriBuilder
            {
                Path = $"v2/path/test/{id}"
            }.ToString();
            using var httpRequest2 = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(httpRequest2);
            var httpResponse2 = await client.SendAsync(httpRequest2);

            response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse2);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Put", response.Method);
            Assert.Equal(id, response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPostRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform post
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Post", response.Method);
            Assert.Equal("eventClientTestId", response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPutWithBadPathAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();
            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform post
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId" // No id allowed for put
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, httpResponse.StatusCode);
            Assert.Throws<InvalidOperationException>(() => httpResponse.ValidateResponse());
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPostAndGetRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform post
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Post", response.Method);
            Assert.Equal("eventClientTestId", response.Id);

            using var httpRequest2 = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(httpRequest2);
            var httpResponse2 = await client.SendAsync(httpRequest2);
            response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse2);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Post", response.Method);
            Assert.Equal("eventClientTestId", response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestPatchAndGetRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform post
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);
            httpResponse.ValidateResponse();

            using var httpRequest2 = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(httpRequest2);
            var httpResponse2 = await client.SendAsync(httpRequest2);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse2);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Patch", response.Method);
            Assert.Equal("eventClientTestId", response.Id);
        }

        [Theory]
        [MemberData(nameof(InMemoryServerFixture.GetSerializers), MemberType = typeof(InMemoryServerFixture))]
        public async Task TestDeleteRequestAsync(ISerializer serializer)
        {
            var client = _fixture.GetHttpClientWithTunnelOverEventClient();

            var expected = new TestRequestModel
            {
                Input = "this is a test"
            };

            // Perform post
            var uri = new UriBuilder
            {
                Path = "v2/path/test/eventClientTestId"
            }.ToString();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(httpRequest, expected);
            var httpResponse = await client.SendAsync(httpRequest);
            var response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse);

            Assert.Equal(expected.Input, response.Input);
            Assert.Equal("Post", response.Method);
            Assert.Equal("eventClientTestId", response.Id);

            using var httpRequest2 = new HttpRequestMessage(HttpMethod.Delete, uri);
            var httpResponse2 = await client.SendAsync(httpRequest2);
            httpResponse2.ValidateResponse();

            using var httpRequest3 = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(httpRequest3);
            var httpResponse3 = await client.SendAsync(httpRequest3);
            response = await serializer.DeserializeResponseAsync<TestResponseModel>(
                httpResponse3);

            Assert.Null(response.Input);
            Assert.Equal("Get", response.Method);
            Assert.Equal("eventClientTestId", response.Id);
        }
    }
}
