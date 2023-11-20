// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Threading.Tasks;

    [Version("_V1")]
    public class TestControllerV1 : IMethodController
    {
        public static Task<TestModel> Test1Async(TestModel request)
        {
            return Task.FromResult(request);
        }
        public static Task<byte[]> Test2Async(byte[] request)
        {
            return Task.FromResult(request);
        }
        public static Task<byte[]> Test3Async(byte[] request, int value)
        {
            if (value == 0)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return Task.FromResult(request);
        }
        public Task<string> TestNoParametersAsync()
        {
            return Task.FromResult(nameof(TestNoParametersAsync));
        }
        public Task TestNoReturnAsync(string input)
        {
            if (input != nameof(TestNoReturnAsync))
            {
                throw new ArgumentNullException(nameof(input));
            }
            return Task.CompletedTask;
        }
        public Task TestNoParametersAndNoReturnAsync()
        {
            _noparamcalled = true;
            return Task.CompletedTask;
        }
#pragma warning disable CA1051 // Do not declare visible instance fields
        public bool _noparamcalled;
#pragma warning restore CA1051 // Do not declare visible instance fields
    }
}
