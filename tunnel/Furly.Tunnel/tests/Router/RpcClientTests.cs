// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using Autofac;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class RpcClientTests
    {
        private readonly IJsonSerializer _serializer = new DefaultJsonSerializer();
        private readonly ITestOutputHelper _output;

        public RpcClientTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Test1InvocationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeObjectToString(expected));

                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test2InvocationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test2_V1",
                    _serializer.SerializeObjectToString(buffer));

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test3_V1",
                    _serializer.SerializeObjectToString(new
                    {
                        request = buffer,
                        value = 55
                    }));

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationV2Async()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                const int expected = 3254;

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test3_v2",
                    _serializer.SerializeObjectToString(new
                    {
                        request = buffer,
                        value = expected
                    }));

                var returned = _serializer.Deserialize<int>(response);
                Assert.Equal(expected, returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    _serializer.SerializeObjectToString(null));

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamWithCancellationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    _serializer.SerializeObjectToString(null));

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoReturn_V1",
                    _serializer.SerializeObjectToString(nameof(TestControllerV1.TestNoReturnAsync)));

                Assert.Empty(response);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnWithCancellationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoReturnC_V1",
                    _serializer.SerializeObjectToString(nameof(TestControllerV1WithCancellationToken.TestNoReturnCAsync)));

                Assert.Empty(response);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnAsync()
        {
            var controller = new TestControllerV1();
            await using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturn_V1",
                    _serializer.SerializeObjectToString(null));

                Assert.Empty(response);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnWithCancellationAsync()
        {
            var controller = new TestControllerV1WithCancellationToken();
            await using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturnC_V1",
                    _serializer.SerializeObjectToString(null));

                Assert.Empty(response);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task Test1InvocationWithSmallBufferUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };
                var response = await rpcClient.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeObjectToString(expected));
                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test1InvocationWithLargeBufferUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };
                var response = await rpcClient.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeToString(expected));
                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test2InvocationWithLargeBufferUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test2_V1",
                    _serializer.SerializeToString(buffer));

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationWithLargeBufferUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test3_V1",
                    _serializer.SerializeToString(new
                    {
                        request = buffer,
                        value = 55
                    })
                );

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationV2WithLargeBufferUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);
                const int expected = 3254;

                var response = await rpcClient.CallMethodAsync("testtarget1", "Test3_V2",
                    _serializer.SerializeToString(new
                    {
                        request = buffer,
                        value = expected
                    })
                );

                var returned = _serializer.Deserialize<int>(response);
                Assert.Equal(expected, returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    _serializer.SerializeObjectToString(null));

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamUsingMethodClientWithCancellationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    _serializer.SerializeObjectToString(null));

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNullParamUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    string.Empty);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNullParamUsingMethodClientWithCancellationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    string.Empty);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnUsingMethodClientAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoReturn_V1",
                    _serializer.SerializeToString(nameof(TestControllerV1.TestNoReturnAsync)));

                Assert.Empty(response);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnUsingMethodClientWithCancellationAsync()
        {
            await using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoReturnC_V1",
                    _serializer.SerializeToString(nameof(TestControllerV1WithCancellationToken.TestNoReturnCAsync)));

                Assert.Empty(response);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnUsingMethodClientAsync()
        {
            var controller = new TestControllerV1();
            await using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturn_V1",
                    _serializer.SerializeObjectToString(null));

                Assert.Empty(response);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnUsingMethodClientWithCancellationAsync()
        {
            var controller = new TestControllerV1WithCancellationToken();
            await using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var rpcClient = services.Resolve<IRpcClient>();

                var response = await rpcClient.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturnC_V1",
                    _serializer.SerializeObjectToString(null));

                Assert.Empty(response);
                Assert.True(controller._noparamcalled);
            }
        }
    }
}
