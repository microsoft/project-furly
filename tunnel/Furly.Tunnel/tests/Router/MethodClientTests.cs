// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using Autofac;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class MethodClientTests
    {
        private readonly DefaultJsonSerializer _serializer = new ();
        private readonly ITestOutputHelper _output;

        public MethodClientTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Test1InvocationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };

                var response = await client.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeObjectToMemory(expected), ContentMimeType.Json);

                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test2InvocationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);

                var response = await client.CallMethodAsync("testtarget1", "Test2_V1",
                    _serializer.SerializeObjectToMemory(buffer), ContentMimeType.Json);

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);

                var response = await client.CallMethodAsync("testtarget1", "Test3_V1",
                    _serializer.SerializeObjectToMemory(new
                    {
                        request = buffer,
                        value = 55
                    }), ContentMimeType.Json);

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationV2Async()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                const int expected = 3254;

                var response = await client.CallMethodAsync("testtarget1", "Test3_v2",
                    _serializer.SerializeObjectToMemory(new
                    {
                        request = buffer,
                        value = expected
                    }), ContentMimeType.Json);

                var returned = _serializer.Deserialize<int>(response);
                Assert.Equal(expected, returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamWithCancellationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoReturn_V1",
                    _serializer.SerializeObjectToMemory(nameof(TestControllerV1.TestNoReturnAsync)),
                    ContentMimeType.Json);

                Assert.Equal(0, response.Length);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnWithCancellationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoReturnC_V1",
                    _serializer.SerializeObjectToMemory(nameof(TestControllerV1WithCancellationToken.TestNoReturnCAsync)),
                    ContentMimeType.Json);

                Assert.Equal(0, response.Length);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnAsync()
        {
            var controller = new TestControllerV1();
            using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturn_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                Assert.Equal(0, response.Length);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnWithCancellationAsync()
        {
            var controller = new TestControllerV1WithCancellationToken();
            using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturnC_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                Assert.Equal(0, response.Length);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task Test1InvocationWithSmallBufferUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[1049];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };
                var response = await client.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeObjectToMemory(expected), ContentMimeType.Json);
                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test1InvocationWithLargeBufferUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);
                var expected = new TestModel { Test = buffer };
                var response = await client.CallMethodAsync("testtarget1", "Test1_V1",
                    _serializer.SerializeToMemory(expected), ContentMimeType.Json);
                var returned = _serializer.Deserialize<TestModel>(response);
                Assert.Equal(expected.Test, returned!.Test);
            }
        }

        [Fact]
        public async Task Test2InvocationWithLargeBufferUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);

                var response = await client.CallMethodAsync("testtarget1", "Test2_V1",
                    _serializer.SerializeToMemory(buffer), ContentMimeType.Json);

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationWithLargeBufferUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);

                var response = await client.CallMethodAsync("testtarget1", "Test3_V1",
                    _serializer.SerializeToMemory(new
                    {
                        request = buffer,
                        value = 55
                    }), ContentMimeType.Json);

                var returned = _serializer.Deserialize<byte[]>(response);
                Assert.True(buffer.SequenceEqual(returned!));
            }
        }

        [Fact]
        public async Task Test3InvocationV2WithLargeBufferUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var buffer = new byte[300809];
                MethodRouterTests.FillRandom(buffer);
                const int expected = 3254;

                var response = await client.CallMethodAsync("testtarget1", "Test3_V2",
                    _serializer.SerializeToMemory(new
                    {
                        request = buffer,
                        value = expected
                    }), ContentMimeType.Json);

                var returned = _serializer.Deserialize<int>(response);
                Assert.Equal(expected, returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNoParamUsingMethodClientWithCancellationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }
        [Fact]
        public async Task TestNoParametersInvocationNullParamUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParameters_V1",
                    default, ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1.TestNoParametersAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoParametersInvocationNullParamUsingMethodClientWithCancellationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersC_V1",
                    default, ContentMimeType.Json);

                var returned = _serializer.Deserialize<string>(response);
                Assert.Equal(nameof(TestControllerV1WithCancellationToken.TestNoParametersCAsync), returned);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnUsingMethodClientAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoReturn_V1",
                    _serializer.SerializeToMemory(nameof(TestControllerV1.TestNoReturnAsync)),
                    ContentMimeType.Json);

                Assert.Equal(0, response.Length);
            }
        }

        [Fact]
        public async Task TestNoReturnInvocationNoReturnUsingMethodClientWithCancellationAsync()
        {
            using (var services = await MethodRouterTests.CreateContainerAsync())
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoReturnC_V1",
                    _serializer.SerializeToMemory(nameof(TestControllerV1WithCancellationToken.TestNoReturnCAsync)),
                    ContentMimeType.Json);

                Assert.Equal(0, response.Length);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnUsingMethodClientAsync()
        {
            var controller = new TestControllerV1();
            using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturn_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                Assert.Equal(0, response.Length);
                Assert.True(controller._noparamcalled);
            }
        }

        [Fact]
        public async Task TestNoParametersAndNoReturnInvocationNoParamAndNoReturnUsingMethodClientWithCancellationAsync()
        {
            var controller = new TestControllerV1WithCancellationToken();
            using (var services = await MethodRouterTests.CreateContainerAsync(controller.YieldReturn()))
            {
                var client = services.Resolve<IMethodClient>();

                var response = await client.CallMethodAsync("testtarget1", "TestNoParametersAndNoReturnC_V1",
                    _serializer.SerializeObjectToMemory(null), ContentMimeType.Json);

                Assert.Equal(0, response.Length);
                Assert.True(controller._noparamcalled);
            }
        }
    }
}
