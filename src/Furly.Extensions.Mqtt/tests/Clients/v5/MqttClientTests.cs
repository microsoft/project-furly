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
    public sealed class MqttClientTests : IDisposable
    {
        private readonly MqttClientHarness _harness;

        public MqttClientTests(MqttServerFixture server, ITestOutputHelper output)
        {
            _harness = new MqttClientHarness(server, output, MqttVersion.v5);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic1Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test3", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1", data, contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic2Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/#", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            // Note: order is not guaranteed due to partitioning, hence capturing only a single through filter
            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test3", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test3", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test3", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic3Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+/test", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic4Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+/test/#", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test/testx/testy", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test/testx/testy", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventTest1Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetPublisherEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
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
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 5)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "1").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "2").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "3").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "4").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "5").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "6").ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(data.Length, result.Data.Length);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
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
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 16)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), "1").ConfigureAwait(false);
            await eventClient.SendEventAsync(target,
                Enumerable.Range(1, 5).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), "2").ConfigureAwait(false);
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableTheory]
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
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == max)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            var rand = new Random();
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, max).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
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
            var eventSubscriber = _harness.GetPublisherEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 4)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "1").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "2").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "3").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "4").ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), "5").ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(data.Length, result.Data.Length);
            data.Should().BeEquivalentTo(result.Data);
        }
    }
}
