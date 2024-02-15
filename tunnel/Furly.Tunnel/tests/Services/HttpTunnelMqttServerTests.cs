// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services.Tests
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using Moq;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [UnitTest]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security",
        "CA5394:Do not use insecure randomness", Justification = "Tests")]
    public sealed class HttpTunnelMqttServerTests : IClassFixture<MqttServerFixture>, IDisposable
    {
        public HttpTunnelMqttServerTests(MqttServerFixture server, ITestOutputHelper output)
        {
            _fixture = new MqttPubSubFixture(server, output);
            _output = output;
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [SkippableFact]
        public async Task TestGetWebAsync()
        {
            Skip.If(true);
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
            {
                var clientPublisher = _fixture.GetPublisherEventClient();
                Skip.If(clientPublisher == null);
                var clientSubscriber = _fixture.GetPublisherEventSubscriber();
                Skip.If(clientSubscriber == null);
                using var clientHandler = new HttpTunnelEventClientHandler(clientPublisher, clientSubscriber, _serializer,
                    _output.ToLogger<HttpTunnelEventClientHandler>());
                var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
                using var client = clientFactory.CreateClient("msft");

                var serverSubscriber = _fixture.GetSubscriberEventSubscriber();
                Skip.If(serverSubscriber == null);
                var httpClientFactoryServer = new HttpTunnelHttpClientFactoryServer(HttpTunnelFixture.CreateHttpClientFactory());
                using var server = new HttpTunnelEventServer(httpClientFactoryServer,
                    serverSubscriber, _serializer, _output.ToLogger<HttpTunnelEventServer>());
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
        }

        [SkippableTheory, CombinatorialData]
        public async Task TestGetAsync(
            [CombinatorialValues(1000 * 1024, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            {
                var clientPublisher = _fixture.GetPublisherEventClient();
                Skip.If(clientPublisher == null);
                var clientSubscriber = _fixture.GetPublisherEventSubscriber();
                Skip.If(clientSubscriber == null);
                using var clientHandler = new HttpTunnelEventClientHandler(clientPublisher, clientSubscriber, _serializer,
                    _output.ToLogger<HttpTunnelEventClientHandler>());
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

                var serverSubscriber = _fixture.GetSubscriberEventSubscriber();
                Skip.If(serverSubscriber == null);
                using var server = new HttpTunnelEventServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock),
                    serverSubscriber, _serializer, _output.ToLogger<HttpTunnelEventServer>());
                await server;

                // Act

                using var result = await client.GetAsync(new Uri("https://test/test/test?test=test")).ConfigureAwait(false);

                // Assert

                Assert.NotNull(result);
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                Assert.NotNull(result.Content);
                Assert.NotNull(result.Content.Headers);
                var payload = await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Assert.Equal(responseBuffer, payload);
                Assert.NotNull(result.Headers);
                Assert.Empty(result.Headers);
            }
        }

        [SkippableTheory, CombinatorialData]
        public async Task TestPostAsync(
            [CombinatorialValues(1000 * 1024, 1, 0)] int requestSize,
            [CombinatorialValues(1000 * 1024, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            {
                var clientPublisher = _fixture.GetPublisherEventClient();
                Skip.If(clientPublisher == null);
                var clientSubscriber = _fixture.GetPublisherEventSubscriber();
                Skip.If(clientSubscriber == null);
                using var clientHandler = new HttpTunnelEventClientHandler(clientPublisher, clientSubscriber, _serializer,
                    _output.ToLogger<HttpTunnelEventClientHandler>());
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

                var serverSubscriber = _fixture.GetSubscriberEventSubscriber();
                Skip.If(serverSubscriber == null);
                using var server = new HttpTunnelEventServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock),
                    serverSubscriber, _serializer, _output.ToLogger<HttpTunnelEventServer>());
                await server;
                var requestBuffer = new byte[requestSize];
                rand.NextBytes(requestBuffer);

                // Act

                using var content = new ByteArrayContent(requestBuffer);
                using var result = await client.PostAsync(uri, content).ConfigureAwait(false);

                // Assert

                Assert.NotNull(result);
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                Assert.NotNull(result.Content);
                Assert.NotNull(result.Content.Headers);
                var payload = await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Assert.Equal(responseBuffer, payload);
                Assert.NotNull(result.Headers);
                Assert.Empty(result.Headers);
            }
        }

        [SkippableTheory, CombinatorialData]
        public async Task TestPutAsync(
            [CombinatorialValues(1000 * 1024, 1, 0)] int requestSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            {
                var clientPublisher = _fixture.GetPublisherEventClient();
                Skip.If(clientPublisher == null);
                var clientSubscriber = _fixture.GetPublisherEventSubscriber();
                Skip.If(clientSubscriber == null);
                using var clientHandler = new HttpTunnelEventClientHandler(clientPublisher, clientSubscriber, _serializer,
                    _output.ToLogger<HttpTunnelEventClientHandler>());
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

                var serverSubscriber = _fixture.GetSubscriberEventSubscriber();
                Skip.If(serverSubscriber == null);
                using var server = new HttpTunnelEventServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock),
                    serverSubscriber, _serializer, _output.ToLogger<HttpTunnelEventServer>());
                await server;
                var requestBuffer = new byte[requestSize];
                rand.NextBytes(requestBuffer);

                // Act

                using var content = new ByteArrayContent(requestBuffer);
                using var result = await client.PutAsync(uri, content).ConfigureAwait(false);

                // Assert

                Assert.NotNull(result);
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                Assert.NotNull(result.Content);
                Assert.NotNull(result.Content.Headers);
                Assert.Equal(0, result.Content.Headers.ContentLength);
                Assert.NotNull(result.Headers);
                Assert.Empty(result.Headers);
            }
        }

        [SkippableFact]
        public async Task TestDeleteAsync()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            {
                var clientPublisher = _fixture.GetPublisherEventClient();
                Skip.If(clientPublisher == null);
                var clientSubscriber = _fixture.GetPublisherEventSubscriber();
                Skip.If(clientSubscriber == null);
                using var clientHandler = new HttpTunnelEventClientHandler(clientPublisher, clientSubscriber, _serializer,
                    _output.ToLogger<HttpTunnelEventClientHandler>());
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
                        r => r.RequestUri == uri && r.Method.Method == "DELETE"),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                var serverSubscriber = _fixture.GetSubscriberEventSubscriber();
                Skip.If(serverSubscriber == null);
                using var server = new HttpTunnelEventServer(new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock),
                    serverSubscriber, _serializer, _output.ToLogger<HttpTunnelEventServer>());
                await server;

                // Act

                using var result = await client.DeleteAsync(uri).ConfigureAwait(false);

                // Assert

                Assert.NotNull(result);
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                Assert.NotNull(result.Content);
                Assert.NotNull(result.Content.Headers);
                Assert.Equal(0, result.Content.Headers.ContentLength);
                Assert.NotNull(result.Headers);
                Assert.Empty(result.Headers);
            }
        }

        private readonly IJsonSerializer _serializer = new DefaultJsonSerializer();
        private readonly MqttPubSubFixture _fixture;
        private readonly ITestOutputHelper _output;
    }
}
