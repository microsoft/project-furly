// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services.Tests
{
    using Furly.Tunnel;
    using Furly.Extensions.Logging;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using Microsoft.Extensions.Logging;
    using Moq;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security",
        "CA5394:Do not use insecure randomness", Justification = "Tests")]
    public class HttpTunnelHybridServerTests
    {
        [Fact]
        public async Task TestGetWebAsync()
        {
            Skip.If(true);
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var eventBridge = new InMemoryEventBroker();
            var chunkServer = new TestRpcServer(_serializer, 100, (target, method, buffer, contentType) =>
            {
                Assert.Equal(MethodNames.Response, method);
                Assert.NotNull(target);
                Assert.NotNull(buffer);
                Assert.NotNull(contentType);
            });

            using var clientHandler = new HttpTunnelHybridClientHandler(
                chunkServer, eventBridge, _serializer, logger.CreateLogger<HttpTunnelHybridClientHandler>());
            var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
            using var client = clientFactory.CreateClient("msft");

            var httpClientFactoryServer = new HttpTunnelHttpClientFactoryServer(HttpTunnelFixture.CreateHttpClientFactory());
            using var server = new HttpTunnelHybridServer(httpClientFactoryServer, eventBridge,
                chunkServer, _serializer, logger);
            await server;

            // Act

            using var result = await client.GetAsync(new Uri("https://www.github.com"));

            // Assert

            Assert.NotNull(result);
            Assert.True(result.IsSuccessStatusCode);
            Assert.NotNull(result.Content);
            Assert.NotNull(result.Content.Headers);
            var payload = await result.Content.ReadAsStringAsync();
            Assert.NotNull(payload);
            Assert.NotNull(result.Headers);
            Assert.True(result.Headers.Any());
            Assert.Contains("</html>", payload, StringComparison.InvariantCultureIgnoreCase);
        }

        [Theory, CombinatorialData]
        public async Task TestGetAsync(
            [CombinatorialValues(5 * 1024 * 1024, 1000 * 1024, 100000, 20, 13, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var eventBridge = new InMemoryEventBroker();
            var chunkServer = new TestRpcServer(_serializer, 100, (target, method, buffer, contentType) =>
            {
                Assert.Equal(MethodNames.Response, method);
                Assert.NotNull(target);
                Assert.NotNull(contentType);
                Assert.NotNull(buffer);
            });

            using var clientHandler = new HttpTunnelHybridClientHandler(
                chunkServer, eventBridge, _serializer, logger.CreateLogger<HttpTunnelHybridClientHandler>());
            var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
            using var client = clientFactory.CreateClient("msft");

            var uri = new Uri("https://test/test/test?test=test");
            var rand = new Random();
            var fix = new Fixture();
            var responseBuffer = new byte[responseSize];
            rand.NextBytes(responseBuffer);
            var response = Mock.Of<HttpResponseMessage>(r =>
                r.Content == new ByteArrayContent(responseBuffer) &&
                r.StatusCode == System.Net.HttpStatusCode.OK);
            var httpclientFactoryMock = Mock.Of<IHttpClientFactory>();
            var httpclientMock = Mock.Of<HttpClient>();
            Mock.Get(httpclientFactoryMock)
                .Setup(m => m.CreateClient(It.IsAny<string>()))
                .Returns(httpclientMock);
            Mock.Get(httpclientMock)
                .Setup(m => m.SendAsync(It.Is<HttpRequestMessage>(
                    r => r.RequestUri == uri && r.Method.Method == "GET"), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));

            using var server = new HttpTunnelHybridServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), eventBridge,
                chunkServer, _serializer, logger);
            await server;

            // Act

            using var result = await client.GetAsync(new Uri("https://test/test/test?test=test"));

            // Assert

            Assert.NotNull(result);
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
            Assert.NotNull(result.Content);
            Assert.NotNull(result.Content.Headers);
            var payload = await result.Content.ReadAsByteArrayAsync();
            Assert.Equal(responseBuffer, payload);
            Assert.NotNull(result.Headers);
            Assert.Empty(result.Headers);
        }

        [Theory, CombinatorialData]
        public async Task TestPostAsync(
            [CombinatorialValues(5 * 1024 * 1024, 1000 * 1024, 100000, 20, 13, 1, 0)] int requestSize,
            [CombinatorialValues(5 * 1024 * 1024, 1000 * 1024, 100000, 20, 13, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var eventBridge = new InMemoryEventBroker();
            var chunkServer = new TestRpcServer(_serializer, 100, (target, method, buffer, contentType) =>
            {
                Assert.Equal(MethodNames.Response, method);
                Assert.NotNull(target);
                Assert.NotNull(contentType);
                Assert.NotNull(buffer);
            });

            using var clientHandler = new HttpTunnelHybridClientHandler(
                chunkServer, eventBridge, _serializer, logger.CreateLogger<HttpTunnelHybridClientHandler>());
            var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
            using var client = clientFactory.CreateClient("msft");

            var uri = new Uri("https://test/test/test?test=test");
            var rand = new Random();
            var fix = new Fixture();
            var responseBuffer = new byte[responseSize];
            rand.NextBytes(responseBuffer);
            var response = Mock.Of<HttpResponseMessage>(r =>
                r.Content == new ByteArrayContent(responseBuffer) &&
                r.StatusCode == System.Net.HttpStatusCode.OK);
            var httpclientFactoryMock = Mock.Of<IHttpClientFactory>();
            var httpclientMock = Mock.Of<HttpClient>();
            Mock.Get(httpclientFactoryMock)
                .Setup(m => m.CreateClient(It.IsAny<string>()))
                .Returns(httpclientMock);
            Mock.Get(httpclientMock)
                .Setup(m => m.SendAsync(It.Is<HttpRequestMessage>(
                    r => r.RequestUri == uri && r.Method.Method == "POST"), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));

            using var server = new HttpTunnelHybridServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), eventBridge,
                chunkServer, _serializer, logger);
            await server;

            var requestBuffer = new byte[requestSize];
            rand.NextBytes(requestBuffer);

            // Act

            using var content = new ByteArrayContent(requestBuffer);
            using var result = await client.PostAsync(uri, content);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
            Assert.NotNull(result.Content);
            Assert.NotNull(result.Content.Headers);
            var payload = await result.Content.ReadAsByteArrayAsync();
            Assert.Equal(responseBuffer, payload);
            Assert.NotNull(result.Headers);
            Assert.Empty(result.Headers);
        }

        [Theory, CombinatorialData]
        public async Task TestPutAsync(
            [CombinatorialValues(5 * 1024 * 1024, 1000 * 1024, 100000, 20, 13, 1, 0)] int requestSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var eventBridge = new InMemoryEventBroker();
            var chunkServer = new TestRpcServer(_serializer, 100, (target, method, buffer, contentType) =>
            {
                Assert.Equal(MethodNames.Response, method);
                Assert.NotNull(target);
                Assert.NotNull(contentType);
                Assert.NotNull(buffer);
            });

            using var clientHandler = new HttpTunnelHybridClientHandler(
                chunkServer, eventBridge, _serializer, logger.CreateLogger<HttpTunnelHybridClientHandler>());
            var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
            using var client = clientFactory.CreateClient("msft");

            var uri = new Uri("https://test/test/test?test=test");
            var rand = new Random();
            var fix = new Fixture();
            var response = Mock.Of<HttpResponseMessage>(r =>
                r.Content == null &&
                r.StatusCode == System.Net.HttpStatusCode.OK);
            var httpclientFactoryMock = Mock.Of<IHttpClientFactory>();
            var httpclientMock = Mock.Of<HttpClient>();
            Mock.Get(httpclientFactoryMock)
                .Setup(m => m.CreateClient(It.IsAny<string>()))
                .Returns(httpclientMock);
            Mock.Get(httpclientMock)
                .Setup(m => m.SendAsync(It.Is<HttpRequestMessage>(
                    r => r.RequestUri == uri && r.Method.Method == "PUT"), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));

            using var server = new HttpTunnelHybridServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), eventBridge,
                chunkServer, _serializer, logger);
            await server;

            var requestBuffer = new byte[requestSize];
            rand.NextBytes(requestBuffer);

            // Act

            using var content = new ByteArrayContent(requestBuffer);
            using var result = await client.PutAsync(uri, content);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
            Assert.NotNull(result.Content);
            Assert.NotNull(result.Content.Headers);
            Assert.Equal(0, result.Content.Headers.ContentLength);
            Assert.NotNull(result.Headers);
            Assert.Empty(result.Headers);
        }

        [Fact]
        public async Task TestDeleteAsync()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var eventBridge = new InMemoryEventBroker();
            var chunkServer = new TestRpcServer(_serializer, 100, (target, method, buffer, contentType) =>
            {
                Assert.Equal(MethodNames.Response, method);
                Assert.NotNull(target);
                Assert.NotNull(buffer);
                Assert.NotNull(contentType);
            });

            using var clientHandler = new HttpTunnelHybridClientHandler(
                chunkServer, eventBridge, _serializer, logger.CreateLogger<HttpTunnelHybridClientHandler>());
            var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
            using var client = clientFactory.CreateClient("msft");

            var uri = new Uri("https://test/test/test?test=test");
            var rand = new Random();
            var fix = new Fixture();
            var response = Mock.Of<HttpResponseMessage>(r =>
                r.Content == null &&
                r.StatusCode == System.Net.HttpStatusCode.OK);
            var httpclientFactoryMock = Mock.Of<IHttpClientFactory>();
            var httpclientMock = Mock.Of<HttpClient>();
            Mock.Get(httpclientFactoryMock)
                .Setup(m => m.CreateClient(It.IsAny<string>()))
                .Returns(httpclientMock);
            Mock.Get(httpclientMock)
                .Setup(m => m.SendAsync(It.Is<HttpRequestMessage>(
                    r => r.RequestUri == uri && r.Method.Method == "DELETE"), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));

            using var server = new HttpTunnelHybridServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock),
                eventBridge, chunkServer, _serializer, logger);
            await server;

            // Act

            using var result = await client.DeleteAsync(uri);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
            Assert.NotNull(result.Content);
            Assert.NotNull(result.Content.Headers);
            Assert.Equal(0, result.Content.Headers.ContentLength);
            Assert.NotNull(result.Headers);
            Assert.Empty(result.Headers);
        }

        private readonly IJsonSerializer _serializer = new DefaultJsonSerializer();
    }
}
