// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.Tests
{
    using Furly.Tunnel.Services;
    using Furly.Azure;
    using Furly.Extensions.Logging;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using Microsoft.Extensions.Logging;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    /// <summary>
    /// Tests a tunnel from IoT Hub to IoT Edge over device methods.
    /// The device method server on the edge unpacks and calls the
    /// http endpoint and returns the result via device method to
    /// the cloud handler.
    /// </summary>
    [SystemTest]
    [Collection(IoTHubServiceCollection.Name)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security",
        "CA5394:Do not use insecure randomness", Justification = "Tests")]
    public class HubToEdgeMethodServerTests : IClassFixture<IoTEdgeEventClientFixture>
    {
        public HubToEdgeMethodServerTests(IoTEdgeEventClientFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task TestGetWebAsync()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var hub = fixture.Create<string>();
            var deviceId = fixture.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                // Get hub side device method based http client
                var hubClient = harness.GetHubRpcClient();
                Skip.If(hubClient == null);
                using var clientHandler = new HttpTunnelMethodClientHandler(hubClient, _serializer, logger);
                clientHandler.Properties.Add("target", resource);
                var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
                using var client = clientFactory.CreateClient("msft");

                // Create module side proxy server
                var httpClientFactoryServer = new HttpTunnelHttpClientFactoryServer(HttpTunnelFixture.CreateHttpClientFactory());
                var connection = harness.GetModuleRpcServer();
                Skip.If(connection == null);
                await using var server = new HttpTunnelMethodServer(connection, httpClientFactoryServer, _serializer,
                    logger.CreateLogger<HttpTunnelMethodServer>());
                await server;

                // Act

                using var result = await client.GetAsync(new Uri("https://www.microsoft.com")).ConfigureAwait(false);

                // Assert

                Assert.NotNull(result);
                Assert.True(result.IsSuccessStatusCode);
                Assert.NotNull(result.Content);
                Assert.NotNull(result.Content.Headers);
                var payload = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                Assert.NotNull(payload);
                Assert.NotNull(result.Headers);
                Assert.True(result.Headers.Any());
                Assert.Contains("<!DOCTYPE html>", payload, StringComparison.InvariantCulture);
            }
        }

        [SkippableTheory, CombinatorialData]
        public async Task TestGetAsync(
            [CombinatorialValues(5 * 1024 * 1024, 1000 * 1024, 100000, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var hub = fixture.Create<string>();
            var deviceId = fixture.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var hubClient = harness.GetHubRpcClient();
                Skip.If(hubClient == null);
                using var clientHandler = new HttpTunnelMethodClientHandler(hubClient, _serializer, logger);
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

                var connection = harness.GetModuleRpcServer();
                Skip.If(connection == null);
                await using var server = new HttpTunnelMethodServer(connection,
                    new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), _serializer,
                    logger.CreateLogger<HttpTunnelMethodServer>());
                await server;

                // Act

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Options.AddOrUpdate("target", resource);
                using var result = await client.SendAsync(request).ConfigureAwait(false);

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
            [CombinatorialValues(1000 * 1024, 100000, 1, 0)] int requestSize,
            [CombinatorialValues(1000 * 1024, 100000, 1, 0)] int responseSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var hub = fixture.Create<string>();
            var deviceId = fixture.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var hubClient = harness.GetHubRpcClient();
                Skip.If(hubClient == null);
                using var clientHandler = new HttpTunnelMethodClientHandler(hubClient, _serializer, logger);
                var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
                using var client = clientFactory.CreateClient("msft");

                var uri = new Uri($"https://{resource}/test/test?test=test");
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

                var connection = harness.GetModuleRpcServer();
                Skip.If(connection == null);
                await using var server = new HttpTunnelMethodServer(connection, new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), _serializer,
                    logger.CreateLogger<HttpTunnelMethodServer>());
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
            [CombinatorialValues(1000 * 1024, 100000, 13, 1, 0)] int requestSize)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            // Setup
            var logger = Log.ConsoleFactory();
            var hub = fixture.Create<string>();
            var deviceId = fixture.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var hubClient = harness.GetHubRpcClient();
                Skip.If(hubClient == null);
                using var clientHandler = new HttpTunnelMethodClientHandler(hubClient, _serializer, logger);
                var clientFactory = HttpTunnelFixture.CreateHttpClientFactory(clientHandler);
                using var client = clientFactory.CreateClient("msft");

                var uri = new Uri($"https://{resource}/test/test?test=test");
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

                var connection = harness.GetModuleRpcServer();
                Skip.If(connection == null);
                await using var server = new HttpTunnelMethodServer(connection, new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), _serializer,
                    logger.CreateLogger<HttpTunnelMethodServer>());
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

            // Setup
            var logger = Log.ConsoleFactory();
            var hub = fixture.Create<string>();
            var deviceId = fixture.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var hubClient = harness.GetHubRpcClient();
                Skip.If(hubClient == null);
                using var clientHandler = new HttpTunnelMethodClientHandler(hubClient, _serializer, logger);
                clientHandler.Properties.Add("target", resource);
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

                var connection = harness.GetModuleRpcServer();
                Skip.If(connection == null);
                await using var server = new HttpTunnelMethodServer(connection,
                    new HttpTunnelHttpClientFactoryServer(httpclientFactoryMock), _serializer, logger.CreateLogger<HttpTunnelMethodServer>());
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
        private readonly IoTEdgeEventClientFixture _fixture;
    }
}
