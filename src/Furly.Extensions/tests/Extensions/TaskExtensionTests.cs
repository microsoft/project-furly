// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Threading.Tasks
{
    using Furly;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class TaskExtensionTests
    {
        [Fact]
        public async Task TestAwaitable1Async()
        {
            var result = await new AwaitableDelay();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TestAwaitable2Async()
        {
            IAwaitable awaitable = new AwaitableDelay();
            await awaitable.AsTask();
        }

        [Fact]
        public async Task TestAwaitable3Async()
        {
            var awaitable = new AwaitableDelay();
            var result = await awaitable.AsTask();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TestAwaitable4Async()
        {
            var awaitable = new AwaitableDelay();
            var result = await awaitable.AsTask();
            Assert.NotNull(result);
        }

        [Fact]
        public void TestAwaitable()
        {
            var result = new AwaitableDelay().GetAwaiter().GetResult();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TestAwaitableArray1Async()
        {
            var result = await new[]
            {
                new AwaitableDelay(),
                new AwaitableDelay(),
                new AwaitableDelay()
            }.WhenAll();

            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
        }

        [Fact]
        public async Task TestAwaitableArray2Async()
        {
            await new IAwaitable[]
            {
                new AwaitableDelay(),
                new CompletedAwaitable(),
                new AwaitableDelay()
            }.WhenAll();
        }
    }

    public sealed class AwaitableDelay : IAwaitable<AwaitableDelay>
    {
        private readonly Task _task;

        public AwaitableDelay()
        {
            _task = Task.Delay(100);
        }

        public IAwaiter<AwaitableDelay> GetAwaiter()
        {
            return _task.AsAwaiter(this);
        }
    }

    public sealed class CompletedAwaitable : IAwaitable<CompletedAwaitable>
    {
        private readonly Task _task;

        public CompletedAwaitable()
        {
            _task = Task.CompletedTask;
        }

        public IAwaiter<CompletedAwaitable> GetAwaiter()
        {
            return _task.AsAwaiter(this);
        }
    }
}
