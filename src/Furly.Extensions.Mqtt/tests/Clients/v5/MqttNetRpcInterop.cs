// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients.v5
{
    using AutoFixture;
    using FluentAssertions;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Extensions.Rpc;
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [SystemTest]
    [Collection(MqttCollection.Name)]
    public sealed class MqttNetRpcInterop : IDisposable
    {
        private readonly MqttClientHarness _harness;

        public MqttNetRpcInterop(MqttServerFixture server, ITestOutputHelper output)
        {
            _harness = new MqttClientHarness(server, output, MqttVersion.v5);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [Fact]
        public async Task CallMethodSimpleTestAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Assert.NotNull(rpcServer);
            using var client = await CreateMqttClientAsync();
            using var rpcClient = CreateRpcClient(client);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver1", args =>
            {
                args.Target.Should().Be(method);
                args.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(input));

                return Encoding.UTF8.GetBytes(output);
            }))).ConfigureAwait(false))
            {
                var result = await rpcClient.ExecuteAsync(TimeSpan.FromMinutes(5), "test/rpcserver1/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                Encoding.UTF8.GetString(result).Should().Be(output);
            }
        }

        [Fact]
        public async Task CallUnsupportedMethodAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Assert.NotNull(rpcServer);
            using var client = await CreateMqttClientAsync();
            using var rpcClient = CreateRpcClient(client);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler(
                "test/rpcserver1", _ => throw new NotSupportedException()))).ConfigureAwait(false))
            {
                var result = await rpcClient.ExecuteAsync(TimeSpan.FromMinutes(5), "test/rpcserver1/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

                // We support interop with mqttnet rpc client by returning a single byte of 0.
                // TODO: Remove once bugs are fixed
                result.Length.Should().Be(1);
                result[0].Should().Be(0);
            }
        }

        [Fact]
        public async Task CallMethodWIthMultipleServersTestAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Assert.NotNull(rpcServer);
            using var client = await CreateMqttClientAsync();
            using var rpcClient = CreateRpcClient(client);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            var servers = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
                await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver" + i, args =>
            {
                args.Target.Should().Be(method);
                args.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(input));

                return Encoding.UTF8.GetBytes(output);
            })).ConfigureAwait(false)).ToArray());
            try
            {
                var result = await rpcClient.ExecuteAsync(TimeSpan.FromSeconds(5), "test/rpcserver1/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                Encoding.UTF8.GetString(result).Should().Be(output);
                result = await rpcClient.ExecuteAsync(TimeSpan.FromSeconds(5), "test/rpcserver6/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                Encoding.UTF8.GetString(result).Should().Be(output);
                result = await rpcClient.ExecuteAsync(TimeSpan.FromSeconds(5), "test/rpcserver5/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                Encoding.UTF8.GetString(result).Should().Be(output);
                result = await rpcClient.ExecuteAsync(TimeSpan.FromSeconds(5), "test/rpcserver7/" + method,
                    input, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                Encoding.UTF8.GetString(result).Should().Be(output);
            }
            finally
            {
                await Task.WhenAll(servers
                    .Select(async s => await s.DisposeAsync().ConfigureAwait(false))
                    .ToArray());
            }
        }

        private static IMqttRpcClient CreateRpcClient(IMqttClient mqttClient)
        {
            var mqttFactory = new MqttFactory();
            return mqttFactory.CreateMqttRpcClient(mqttClient,
                new MqttRpcClientOptionsBuilder().WithTopicGenerationStrategy(new PublisherTopicGenerationStrategy()).Build());
        }

        private static async Task<IMqttClient> CreateMqttClientAsync()
        {
            var mqttFactory = new MqttFactory();
            var mqttClient = mqttFactory.CreateMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883)
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .Build();
            await mqttClient.ConnectAsync(mqttClientOptions).ConfigureAwait(false);
            return mqttClient;
        }

        private sealed class PublisherTopicGenerationStrategy : IMqttRpcClientTopicGenerationStrategy
        {
            public MqttRpcTopicPair CreateRpcTopics(TopicGenerationContext context)
            {
                return new()
                {
                    RequestTopic = context.MethodName,
                    ResponseTopic = $"{context.MethodName}/response"
                };
            }
        }
    }
}
