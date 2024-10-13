// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    [Version("_V1")]
    [Version("_V2")]
    public class TestControllerAsyncEnumerable : IMethodController
    {
        public static async IAsyncEnumerable<byte[]> Enumerate1Async(byte[] request)
        {
            foreach (var item in request)
            {
                await Task.Delay(0);
                yield return new byte[] { item };
            }
        }
        public static async IAsyncEnumerable<byte[]> Enumerate2Async(byte[] request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var item in request)
            {
                await Task.Delay(0, ct);
                yield return new byte[] { item };
            }
        }
    }
}
