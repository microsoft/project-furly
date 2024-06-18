// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using Furly.Exceptions;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    [Version("_V2")]
    [TestExceptionFilter]
    public class TestControllerV2WithExceptionFilter : IMethodController
    {
        public static Task<byte[]> Test4Async(byte[] Test4)
        {
            ArgumentNullException.ThrowIfNull(Test4);
            return Task.FromException<byte[]>(
                new ArgumentNullException(nameof(Test4)));
        }

        public static Task<int> Test5Async(byte[] request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromException<int>(
                new System.IO.IOException("Test5"));
        }

        public static Task<int> Test6Async(byte[] request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromException<int>(
                new MethodCallStatusException(506, "Test6"));
        }

        public static async Task<int> Test7Async(byte[] request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(1000);
            await Task.Delay(TimeSpan.FromDays(1), cts.Token);
            return -1;
        }
    }
}
