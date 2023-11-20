// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Net.Sockets
{
    using Furly.Extensions.Utils;

    /// <summary>
    /// Socket extensions
    /// </summary>
    public static class SocketEx
    {
        /// <summary>
        /// Safe close and dispose of socket
        /// </summary>
        /// <param name="socket"></param>
        public static void SafeDispose(this Socket? socket)
        {
            if (socket == null)
            {
                return;
            }
            Try.Op(() => socket.Close(0));
            Try.Op(socket.Dispose);
        }
    }
}
