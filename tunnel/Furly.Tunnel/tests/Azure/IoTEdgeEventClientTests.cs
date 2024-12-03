// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.Tests
{
    using Furly.Azure;
    using Furly.Extensions.Messaging;
    using AutoFixture;
    using FluentAssertions;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [SystemTest]
    [Collection(IoTHubServiceCollection.Name)]
    public class IoTEdgeEventClientTests : IClassFixture<IoTEdgeEventClientFixture>
    {
        private readonly IoTEdgeEventClientFixture _fixture;

        public IoTEdgeEventClientTests(IoTEdgeEventClientFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task SendDeviceEventAndSubscribeWithTopic1Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var eventClient = harness.GetModuleEventClient();
                Skip.If(eventClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var eventSubscriber = harness.GetHubEventSubscriber();
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
        }

        [SkippableFact]
        public async Task SendDeviceEventAndSubscribeWithTopic2Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var eventClient = harness.GetModuleEventClient();
                Skip.If(eventClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var eventSubscriber = harness.GetHubEventSubscriber();
                Skip.If(eventSubscriber == null);
                await eventSubscriber.SubscribeAsync("test/#", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

                await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
                await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
                await eventClient.SendEventAsync("test/test1/test3", data, contentType).ConfigureAwait(false);
                await eventClient.SendEventAsync("test3", data, fix.Create<string>()).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal("test/test1/test3", result.Target);
                Assert.Equal(contentType, result.ContentType);
                data.Should().BeEquivalentTo(result.Data);
            }
        }

        [SkippableFact]
        public async Task SendDeviceEventAndSubscribeWithTopic3Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var eventClient = harness.GetModuleEventClient();
                Skip.If(eventClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var eventSubscriber = harness.GetHubEventSubscriber();
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
        }

        [SkippableFact]
        public async Task SendDeviceEventAndSubscribeWithTopic4Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var eventClient = harness.GetModuleEventClient();
                Skip.If(eventClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var eventSubscriber = harness.GetHubEventSubscriber();
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
        }

        [SkippableFact]
        public async Task SendDeviceEventTest1Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                Assert.True(data.SequenceEqualsSafe([.. result.Data]));
            }
        }

        [SkippableFact]
        public async Task SendDeviceEventTest2Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == 5)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                Assert.True(data.SequenceEqualsSafe([.. result.Data]));
            }
        }

        [SkippableFact]
        public async Task SendDeviceEventTestBatch1Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == 16)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(1, 5).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                Assert.True(data.SequenceEqualsSafe([.. result.Data]));
            }
        }

        [SkippableTheory]
        [InlineData(10)]
        // [InlineData(50)]
        // [InlineData(100)]
        // [InlineData(1000)]
        public async Task SendDeviceEventTestBatch2Async(int max)
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, null);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == max)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                var rand = new Random();
                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, max).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                Assert.True(data.SequenceEqualsSafe([.. result.Data]));
            }
        }

        [SkippableFact]
        public async Task SendModuleEventTest1Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var moduleId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, moduleId);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();
                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                data.Should().BeEquivalentTo(result.Data);
            }
        }

        [SkippableFact]
        public async Task SendModuleEventTest2Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var moduleId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, moduleId);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == 4)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                data.Should().BeEquivalentTo(result.Data);
            }
        }

        [SkippableFact]
        public async Task SendModuleEventTestBatch1Async()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var moduleId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, moduleId);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == 19)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(1, 5).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                data.Should().BeEquivalentTo(result.Data);
            }
        }

        [SkippableTheory]
        [InlineData(10)]
        // [InlineData(50)]
        // [InlineData(100)]
        // [InlineData(1000)]
        public async Task SendModuleEventTestBatch2Async(int max)
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var deviceId = fix.Create<string>();
            var moduleId = fix.Create<string>();
            var resource = HubResource.Format(hub, deviceId, moduleId);
            var harness = _fixture.GetHarness(resource);
            await using (harness.ConfigureAwait(false))
            {
                var moduleClient = harness.GetModuleEventClient();
                Skip.If(moduleClient == null);

                var data = fix.CreateMany<byte>().ToArray();

                var contentType = fix.Create<string>();
                var target = fix.Create<string>();

                var count = 0;
                var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
                var hubClient = harness.GetHubEventSubscriber();
                Skip.If(hubClient == null);
                await hubClient.SubscribeAsync(target, new CallbackConsumer(arg =>
                {
                    if (++count == max)
                    {
                        tcs.TrySetResult(arg);
                    }
                })).ConfigureAwait(false);

                await moduleClient.SendEventAsync(target,
                    Enumerable.Range(0, max).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

                var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
                Assert.Equal(target, result.Target);
                Assert.Equal(contentType, result.ContentType);
                data.Should().BeEquivalentTo(result.Data);
            }
        }
    }
}
