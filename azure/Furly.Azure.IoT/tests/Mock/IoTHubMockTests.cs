// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock.Services
{
    using Furly.Azure.IoT.Models;
    using Furly.Azure.IoT.Services;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using Furly.Extensions.Serializers.Newtonsoft;
    using FluentAssertions;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    /// <summary>
    /// Tests the mock implementation of the IoT hub services
    /// </summary>
    [UnitTest]
    public class IoTHubMockTests
    {
#pragma warning disable CA1024 // Use properties where appropriate
        public static IEnumerable<object[]> GetSerializers()
#pragma warning restore CA1024 // Use properties where appropriate
        {
            yield return new object[] { new DefaultJsonSerializer() };
            yield return new object[] { new NewtonsoftJsonSerializer() };
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task TestConnectDisconnectTestsAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            // Cannot connect twice
            var connection2 = mock.Connect(result.Id);
            connection2.Should().BeNull();

            connection.Close();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Disconnected");

            connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task TestReportPropertyTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");
            connection.Twin.State["_state_"] = "Ok";

            result = await services.GetAsync(result.Id);
            result.Reported["_state_"].Should().Be("Ok");

            result = await services.PatchAsync(new DeviceTwinModel
            {
                Id = result.Id,
                ModuleId = result.ModuleId,
                Desired = new Dictionary<string, VariantValue>
                {
                    ["Power"] = 100
                }
            });

            connection.Twin.State["Power"].Should().Be(100);
            connection.Twin.State["_state_"].Should().Be("Ok");

            connection.Twin.State["Power"] = 100;
            result = await services.GetAsync(result.Id);
            result.Reported["Power"].Should().Be(100);
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task CallDeviceMethodOnConnectedDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            const string expected = "This is the request message!";
            var client = (IRpcClient)mock;
            await using (await connection.RpcServer.ConnectAsync(new FuncDelegate("default",
                (method, payload, contentType, _) =>
                {
                    method.Should().Be("testmethod");
                    contentType.Should().Be("application/json");
                    return payload;
                })))
            {
                var response = await client.CallDeviceMethodAsync(result.Id,
                    "testmethod", expected);
                response.Should().Be(expected);
            }
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task CallDeviceMethodOnDisconnectedDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();

            const string expected = "This is the request message!";
            var client = (IRpcClient)mock;
            await using (await connection.RpcServer.ConnectAsync(new FuncDelegate("default",
                (_, payload, _, _) => payload)))
            {
                connection.Close();

                result = await services.GetAsync(result.Id);
                result.ConnectionState.Should().Be("Disconnected");

                await client.Awaiting(client => client.CallDeviceMethodAsync(result.Id,
                    "testmethod", expected)).Should().ThrowAsync<TimeoutException>();
            }
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task CallDeviceMethodOnDeletedDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            const string expected = "This is the request message!";
            var client = (IRpcClient)mock;
            await using (await connection.RpcServer.ConnectAsync(new FuncDelegate("default",
                (_, payload, _, _) => payload)))
            {
                // Delete device
                await services.DeleteAsync(result.Id);

                await client.Awaiting(client => client.CallDeviceMethodAsync(result.Id,
                    "testmethod", expected)).Should().ThrowAsync<ResourceNotFoundException>();
            }
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task SendEventAsDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            var processor = GetProcessor(mock);
            const string expected = "This is the event message!";
            const int count = 1000;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = processor.Register(new IoTHubTelemetryHandler(arg =>
            {
                arg.Topic.Should().Be("topic" + (arg.Count - 1));
                arg.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(expected));
                arg.ContentType.Should().Be("application/json");
                if (arg.Count == count)
                {
                    tcs.SetResult();
                }
            }));

            for (var i = 0; i < count; i++)
            {
                await connection.EventClient.SendEventAsync("topic" + i, Encoding.UTF8.GetBytes(expected),
                    "application/json", "utf-8");
            }
            await tcs.Task;
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task SendEventFromDisconnectedDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            var processor = GetProcessor(mock);
            using var registration = processor.Register(new IoTHubTelemetryHandler(arg => { }));

            // Disconnect
            connection.Close();

            await connection.EventClient.Invoking(client => client.SendEventAsync("topic",
                Encoding.UTF8.GetBytes("test"), "application/json", "utf-8").AsTask())
                .Should().ThrowAsync<InvalidOperationException>();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task SendEventFromDeletedDeviceTestAsync(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var connection = mock.Connect(result.Id);
            connection.Should().NotBeNull();
            result = await services.GetAsync(result.Id);
            result.ConnectionState.Should().Be("Connected");

            var processor = GetProcessor(mock);
            using var registration = processor.Register(new IoTHubTelemetryHandler(arg => { }));

            // Delete device
            await services.DeleteAsync(result.Id);

            await connection.EventClient.Invoking(client => client.SendEventAsync("topic",
                Encoding.UTF8.GetBytes("test"), "application/json", "utf-8").AsTask())
                .Should().ThrowAsync<InvalidOperationException>();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest1Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);
            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().ContainSingle().Which.Should().NotBeNull();
            var item = devices.Items[0];
            item.Tags.Should().ContainKey("a").And.ContainKey("b");
            item.Desired.Should().ContainKey("temperature").WhoseValue.Should().Be(100);
            item.PrimaryKey.Should().BeNull();
            item.Reported.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest2Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES where properties.desired.temperature >= 100", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().ContainSingle().Which.Should().NotBeNull();
            var item = devices.Items[0];
            item.Tags.Should().ContainKey("a").And.ContainKey("b");
            item.Desired.Should().ContainKey("temperature").WhoseValue.Should().Be(100);
            item.PrimaryKey.Should().BeNull();
            item.Reported.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest3Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES where properties.desired.temperature > 100", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest4Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES where tags.a = 55 AND NOT IS_DEFINED(tags.c)", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().ContainSingle().Which.Should().NotBeNull();
            var item = devices.Items[0];
            item.Tags.Should().ContainKey("a").And.ContainKey("b");
            item.Desired.Should().ContainKey("temperature").WhoseValue.Should().Be(100);
            item.PrimaryKey.Should().BeNull();
            item.Reported.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest5Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES where tags.b.stringTest = 'string' AND tags.b.test = 66", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().ContainSingle().Which.Should().NotBeNull();
            var item = devices.Items[0];
            item.Tags.Should().ContainKey("a").And.ContainKey("b");
            item.Desired.Should().ContainKey("temperature").WhoseValue.Should().Be(100);
            item.PrimaryKey.Should().BeNull();
            item.Reported.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(GetSerializers))]
        public async Task QueryDeviceTwinTest6Async(IJsonSerializer serializer)
        {
            using var mock = new IoTHubMock(serializer);

            var services = GetServices(mock);
            var result = await CreateDeviceAsync(services, serializer);

            var devices = await services.QueryDeviceTwinsAsync(
                "SELECT * FROM DEVICES where tags.b.stringTest = 'string' OR tags.a = 66", null);
            devices.Should().NotBeNull();
            devices.ContinuationToken.Should().BeNull();
            devices.Items.Should().ContainSingle().Which.Should().NotBeNull();
            var item = devices.Items[0];
            item.Tags.Should().ContainKey("a").And.ContainKey("b");
            item.Desired.Should().ContainKey("temperature").WhoseValue.Should().Be(100);
            item.PrimaryKey.Should().BeNull();
            item.Reported.Should().BeEmpty();
        }

        /// <summary>
        /// Create a device in the registry
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static async Task<DeviceTwinModel> CreateDeviceAsync(IIoTHubTwinServices services, IJsonSerializer serializer)
        {
            var device = await services.CreateOrUpdateAsync(new DeviceTwinModel
            {
                Id = "TestDevice",
                Tags = new Dictionary<string, VariantValue>
                {
                    ["a"] = 55,
                    ["b"] = serializer.FromObject(new
                    {
                        test = 66,
                        stringTest = "string"
                    })
                },
                Desired = new Dictionary<string, VariantValue>
                {
                    ["temperature"] = 100
                },
            }, false).ConfigureAwait(false);

            device.Should().NotBeNull();
            device.Tags.Should().NotBeNull();
            device.Desired.Should().NotBeNullOrEmpty();
            device.Reported.Should().BeEmpty();
            device.ConnectionState.Should().Be("Disconnected");
            device.PrimaryKey.Should().NotBeNull();
            device.SecondaryKey.Should().NotBeNull();
            return device;
        }

        public static IIoTHubEventProcessor GetProcessor(IoTHubMock mock)
        {
            return mock;
        }

        public static IIoTHubTwinServices GetServices(IoTHubMock mock)
        {
            return mock;
        }
    }
}
