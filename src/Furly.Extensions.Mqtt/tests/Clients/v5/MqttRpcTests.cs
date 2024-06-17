// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients.v5
{
    using Furly.Extensions.Rpc;
    using Furly.Exceptions;
    using AutoFixture;
    using FluentAssertions;
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;
    using System.Threading;
    using Furly.Extensions.Utils;

    [SystemTest]
    [Collection(MqttCollection.Name)]
    public sealed class MqttRpcTests : IDisposable
    {
        private readonly MqttClientHarness _harness;

        public MqttRpcTests(MqttServerFixture server, ITestOutputHelper output)
        {
            _harness = new MqttClientHarness(server, output, MqttVersion.v5);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [SkippableFact]
        public async Task CallMethodSimpleTestAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver1", args =>
            {
                args.Target.Should().Be(method);
                args.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(input));

                return Encoding.UTF8.GetBytes(output);
            })).ConfigureAwait(false)).ConfigureAwait(false))
            {
                var result = await rpcClient.CallMethodAsync("test/rpcserver1", method, input).ConfigureAwait(false);
                result.Should().Be(output);
            }
        }

        [SkippableFact]
        public async Task CallUnsupportedMethodAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler(
                "test/rpcserver1", _ => throw new NotSupportedException())).ConfigureAwait(false)).ConfigureAwait(false))
            {
                (await rpcClient.Invoking(r => r.CallMethodAsync("test/rpcserver1", method, input).AsTask())
                    .Should().ThrowAsync<MethodCallStatusException>().ConfigureAwait(false)).Which.Result.Should().Be(500);
            }
        }

        [SkippableFact]
        public async Task CallMethodWIthMultipleServersTestAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            var servers = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
                await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver" + i, args =>
            {
                args.Target.Should().Be(method);
                args.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(input));

                return Encoding.UTF8.GetBytes(output);
            })).ConfigureAwait(false)).ToArray()).ConfigureAwait(false);
            try
            {
                var result = await rpcClient.CallMethodAsync("test/rpcserver1", method, input).ConfigureAwait(false);
                result.Should().Be(output);
                result = await rpcClient.CallMethodAsync("test/rpcserver6", method, input).ConfigureAwait(false);
                result.Should().Be(output);
                result = await rpcClient.CallMethodAsync("test/rpcserver5", method, input).ConfigureAwait(false);
                result.Should().Be(output);
                result = await rpcClient.CallMethodAsync("test/rpcserver7", method, input).ConfigureAwait(false);
                result.Should().Be(output);
            }
            finally
            {
                await Task.WhenAll(servers
                    .Select(async s => await s.DisposeAsync().ConfigureAwait(false))
                    .ToArray()).ConfigureAwait(false);
            }
        }

        [SkippableFact]
        public async Task CallUnsupportedMethodWIthMultipleServersTestAsync()
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            var servers = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
                await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver" + i, args =>
                {
                    args.Target.Should().Be(method);
                    args.Data.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(input));

                    return Encoding.UTF8.GetBytes(output);
                })).ConfigureAwait(false)).ToArray()).ConfigureAwait(false);
            try
            {
                var result = await rpcClient.CallMethodAsync("test/rpcserver7", method + "2", input).ConfigureAwait(false);
                false.Should().Be(true);
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<MethodCallStatusException>().Which.Result.Should().Be(405);
            }
            finally
            {
                await Task.WhenAll(servers
                    .Select(async s => await s.DisposeAsync().ConfigureAwait(false))
                    .ToArray()).ConfigureAwait(false);
            }
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        // [InlineData(10000)]
        public async Task CallBlockingMethodWithTimeoutAsync(int callTimeout)
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            using var timeout = new CancellationTokenSource();
            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver1", args =>
            {
                Try.Async(() => Task.Delay(TimeSpan.FromMinutes(10), timeout.Token)).GetAwaiter().GetResult();
                return Encoding.UTF8.GetBytes(output);
            })).ConfigureAwait(false)).ConfigureAwait(false))
            {
                try
                {
                    await rpcClient.CallMethodAsync("test/rpcserver1", method, input,
                        TimeSpan.FromMilliseconds(callTimeout)).ConfigureAwait(false);
                    false.Should().Be(true);
                }
                catch (Exception ex)
                {
                    ex.Should().BeOfType<MethodCallException>();
                }
            }
            await timeout.CancelAsync();
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        // [InlineData(10000)]
        public async Task CallBlockingMethodWithCancellationTokenAsync(int callTimeout)
        {
            var fix = new Fixture();
            var rpcServer = _harness.GetRpcServer();
            Skip.If(rpcServer == null);
            var rpcClient = _harness.GetRpcClient();
            Skip.If(rpcClient == null);

            var method = fix.Create<string>();
            var input = fix.Create<string>();
            var output = fix.Create<string>();

            using var timeout = new CancellationTokenSource();
            await using (var s = (await rpcServer.ConnectAsync(new CallbackHandler("test/rpcserver1", args =>
            {
                Try.Async(() => Task.Delay(TimeSpan.FromMinutes(10), timeout.Token)).GetAwaiter().GetResult();
                return Encoding.UTF8.GetBytes(output);
            })).ConfigureAwait(false)).ConfigureAwait(false))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(callTimeout));
                try
                {
                    await rpcClient.CallMethodAsync("test/rpcserver1", method, input, ct: cts.Token).ConfigureAwait(false);
                    false.Should().Be(true);
                }
                catch (Exception ex)
                {
                    ex.Should().BeAssignableTo<OperationCanceledException>();
                }
            }
            await timeout.CancelAsync();
        }
    }
}
