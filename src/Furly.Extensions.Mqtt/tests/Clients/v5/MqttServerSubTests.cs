// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients.v5
{
    using Furly.Extensions.Messaging;
    using AutoFixture;
    using FluentAssertions;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [SystemTest]
    [Collection(MqttCollection.Name)]
    public sealed class MqttServerSubTests : IDisposable
    {
        private readonly MqttServerFixture _server;
        private readonly MqttClientHarness _harness;

        public MqttServerSubTests(MqttServerFixture server, ITestOutputHelper output)
        {
            _server = server;
            _harness = new MqttClientHarness(server, output, MqttVersion.v5);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [Fact]
        public async Task SendEventTest1Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _server.Subscriber;
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg => tcs.TrySetResult(arg)));

            await eventClient.SendEventAsync(target, data, contentType);

            var result = await tcs.Task.With2MinuteTimeout();
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [Fact]
        public async Task SendEventTest2Async()
        {
            var fix = new Fixture();
            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _server.Subscriber;
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 5)
                {
                    tcs.TrySetResult(arg);
                }
            }));

            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "1");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "2");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "3");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "4");
            await eventClient.SendEventAsync(target, data, contentType);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "5");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "6");

            var result = await tcs.Task.With2MinuteTimeout();
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(data.Length, result.Data.Length);
            data.Should().BeEquivalentTo(result.Data);
        }

        [Fact]
        public async Task SendEventTestBatch1Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _server.Subscriber;
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 16)
                {
                    tcs.TrySetResult(arg);
                }
            }));

            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), "1");
            await eventClient.SendEventAsync(target,
                Enumerable.Range(1, 5).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), "2");
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)data), contentType);

            var result = await tcs.Task.With2MinuteTimeout();
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [Theory]
        [InlineData(10)]
        // [InlineData(50)]
        // [InlineData(100)]
        // [InlineData(1000)]
        public async Task SendEventTestBatch2Async(int max)
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _server.Subscriber;
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == max)
                {
                    tcs.TrySetResult(arg);
                }
            }));

            var rand = new Random();
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, max).Select(_ => (ReadOnlyMemory<byte>)data), contentType);

            var result = await tcs.Task.With2MinuteTimeout();
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [Fact]
        public async Task SendEventTest3Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _server.Subscriber;
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 4)
                {
                    tcs.TrySetResult(arg);
                }
            }));

            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "1");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "2");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "3");
            await eventClient.SendEventAsync(target, data, contentType);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "4");
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "5");

            var result = await tcs.Task.With2MinuteTimeout();
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(data.Length, result.Data.Length);
            data.Should().BeEquivalentTo(result.Data);
        }
    }
}
