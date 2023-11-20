// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    [Version("_V1")]
    public class TestControllerV1WithCancellationToken : IMethodController
    {
        public static Task<TestModel> Test1CAsync(TestModel request,
#pragma warning disable IDE0060 // Remove unused parameter
            CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.FromResult(request);
        }
#pragma warning disable IDE0060 // Remove unused parameter
        public static Task<byte[]> Test2CAsync(byte[] request, CancellationToken ct)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.FromResult(request);
        }
        public static Task<byte[]> Test3CAsync(byte[] request, int value,
#pragma warning disable IDE0060 // Remove unused parameter
            CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            if (value == 0)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return Task.FromResult(request);
        }
        public Task<string> TestNoParametersCAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(nameof(TestNoParametersCAsync));
        }
        public Task TestNoReturnCAsync(string input, CancellationToken cancellationToken)
        {
            if (input != nameof(TestNoReturnCAsync))
            {
                throw new ArgumentNullException(nameof(input));
            }
            return Task.CompletedTask;
        }
#pragma warning disable IDE0060 // Remove unused parameter
        public Task TestNoParametersAndNoReturnCAsync(CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            _noparamcalled = true;
            return Task.CompletedTask;
        }
#pragma warning disable CA1051 // Do not declare visible instance fields
        public bool _noparamcalled;
#pragma warning restore CA1051 // Do not declare visible instance fields
    }
}
