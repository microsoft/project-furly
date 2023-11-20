// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Threading.Tasks;

    [Version("_V2")]
    public class TestControllerV2 : IMethodController
    {
        public static Task<byte[]> Test2Async(byte[] request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromException<byte[]>(
                new ArgumentNullException(nameof(request)));
        }

        public static Task<int> Test3Async(byte[] request, int value)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(value);
        }
    }
}
