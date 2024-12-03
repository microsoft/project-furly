// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Threading.Tasks;

    [Version("_V1")]
    [Version("_V2")]
    public class TestControllerValueTaskV1And2 : IMethodController
    {
        public static ValueTask<byte[]> Value1Async(byte[] request)
        {
            return ValueTask.FromResult(request);
        }
#pragma warning disable IDE0060 // Remove unused parameter
        public static ValueTask Value2Async(byte[] request)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return ValueTask.CompletedTask;
        }
    }
}
