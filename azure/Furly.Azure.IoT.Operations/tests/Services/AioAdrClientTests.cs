// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
#nullable disable
namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Connector.Files;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
    using Microsoft.Extensions.Logging;
    using Moq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class AioAdrClientTests
    {
        [Fact]
        public void DevicesReturnsDeviceNames()
        {
            var expected = new[] { "dev1", "dev2" };
            _clientWrapperMock.Setup(c => c.GetDeviceNames()).Returns(expected);
            using var client = CreateClient();
            Assert.Equal(expected, client.Devices);
        }

        [Fact]
        public void DisposeCallsDisposeAsyncOnUnderlyingClient()
        {
            _clientWrapperMock.Setup(c => c.UnobserveAllAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _clientWrapperMock.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var client = CreateClient();
            client.Dispose();
            _clientWrapperMock.Verify(s => s.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsyncCallsUnobserveAllAndDisposeAsync()
        {
            _clientWrapperMock.Setup(c => c.UnobserveAllAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _clientWrapperMock.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var client = CreateClient();
            await client.DisposeAsync();
            _clientWrapperMock.Verify(c => c.UnobserveAllAsync(It.IsAny<CancellationToken>()), Times.Once);
            _clientWrapperMock.Verify(s => s.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task StartMonitoringAssetsAsyncCallsObserveAssets()
        {
            await using var client = CreateClient();
            _clientWrapperMock.Setup(c => c.ObserveAssets("dev", "ep"));
            await client.StartMonitoringAssetsAsync("dev", "ep", CancellationToken.None);
            _clientWrapperMock.Verify(c => c.ObserveAssets("dev", "ep"), Times.Once);
        }

        [Fact]
        public async Task StopMonitoringAssetsAsyncCallsUnobserveAssetsAsync()
        {
            await using var client = CreateClient();
            _clientWrapperMock.Setup(c => c.UnobserveAssetsAsync("dev", "ep", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            await client.StopMonitoringAssetsAsync("dev", "ep", CancellationToken.None);
            _clientWrapperMock.Verify(c => c.UnobserveAssetsAsync("dev", "ep", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetEndpointCredentialsDelegatesToClient()
        {
            var endpoint = new InboundEndpointSchemaMapValue();
            var creds = new EndpointCredentials();
            _clientWrapperMock.Setup(c => c.GetEndpointCredentials("dev", "ep", endpoint)).Returns(creds);
            using var client = CreateClient();
            Assert.Equal(creds, client.GetEndpointCredentials("dev", "ep", endpoint));
        }

        [Fact]
        public async Task UpdateDeviceStatusAsyncDelegatesToClient()
        {
            var status = new DeviceStatus();
            _clientWrapperMock.Setup(c => c.UpdateDeviceStatusAsync("dev", "ep", status, null, It.IsAny<CancellationToken>())).ReturnsAsync(status);
            await using var client = CreateClient();
            var result = await client.UpdateDeviceStatusAsync("dev", "ep", status, null, CancellationToken.None);
            Assert.Equal(status, result);
        }

        [Fact]
        public async Task UpdateAssetStatusAsyncDelegatesToClient()
        {
            var req = new UpdateAssetStatusRequest
            {
                AssetName = "asset",
                AssetStatus = new AssetStatus()
            };
            var status = new AssetStatus();
            _clientWrapperMock.Setup(c => c.UpdateAssetStatusAsync("dev", "ep",
                It.Is<UpdateAssetStatusRequest>(r => r.AssetName == "asset"),
                null, It.IsAny<CancellationToken>())).ReturnsAsync(status);
            await using var client = CreateClient();
            var result = await client.UpdateAssetStatusAsync("dev", "ep", "asset", new AssetStatus(),
                null, CancellationToken.None);
            Assert.Equal(status, result);
        }

        [Fact]
        public async Task ReportDiscoveredAssetAsyncDelegatesToDiscovery()
        {
            var resp = new CreateOrUpdateDiscoveredAssetResponsePayload { DiscoveredAssetResponse = new DiscoveredAssetResponseSchema() };
            _clientWrapperMock.Setup(s => s.CreateOrUpdateDiscoveredAssetAsync("dev", "ep",
                It.IsAny<CreateOrUpdateDiscoveredAssetRequest>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(resp);
            await using var client = CreateClient();
            var result = await client.ReportDiscoveredAssetAsync("dev", "ep", "asset", new DiscoveredAsset
            {
                DeviceRef = new AssetDeviceRef
                {
                    DeviceName = "dev",
                    EndpointName = "ep"
                }
            }, null, CancellationToken.None);
            Assert.Equal(resp.DiscoveredAssetResponse, result);
        }

        [Fact]
        public async Task ReportDiscoveredDeviceAsyncDelegatesToDiscovery()
        {
            var resp = new CreateOrUpdateDiscoveredDeviceResponsePayload { DiscoveredDeviceResponse = new DiscoveredDeviceResponseSchema() };
            _clientWrapperMock.Setup(s => s.CreateOrUpdateDiscoveredDeviceAsync(It.IsAny<CreateOrUpdateDiscoveredDeviceRequestSchema>(),
                "type", null, It.IsAny<CancellationToken>())).ReturnsAsync(resp);
            await using var client = CreateClient();
            var result = await client.ReportDiscoveredDeviceAsync("dev", new DiscoveredDevice(), "type", null, CancellationToken.None);
            Assert.Equal(resp.DiscoveredDeviceResponse, result);
        }

        [Fact]
        public void GetAssetNamesDelegatesToClient()
        {
            var expected = new[] { "a1", "a2" };
            _clientWrapperMock.Setup(c => c.GetAssetNames("dev", "ep")).Returns(expected);
            using var client = CreateClient();
            Assert.Equal(expected, client.GetAssetNames("dev", "ep"));
        }

        [Fact]
        public void GetInboundEndpointNamesDelegatesToClient()
        {
            var expected = new[] { "ep1", "ep2" };
            _clientWrapperMock.Setup(c => c.GetInboundEndpointNames("dev")).Returns(expected);
            using var client = CreateClient();
            Assert.Equal(expected, client.GetInboundEndpointNames("dev"));
        }

        private AioAdrClient CreateClient()
        {
            _sdkMock.Setup(s => s.CreateAdrClientWrapper(It.IsAny<IMqttPubSubClient>()))
                .Returns(_clientWrapperMock.Object);
            _mqttClientMock.SetupGet(m => m.ClientId).Returns("test-client");
            return new AioAdrClient(_sdkMock.Object, _mqttClientMock.Object, _loggerMock.Object);
        }

        private readonly Mock<IAioSdk> _sdkMock = new();
        private readonly Mock<IMqttPubSubClient> _mqttClientMock = new();
        private readonly Mock<ILogger<AioAdrClient>> _loggerMock = new();
        private readonly Mock<IAdrClientWrapper> _clientWrapperMock = new();
    }
}
