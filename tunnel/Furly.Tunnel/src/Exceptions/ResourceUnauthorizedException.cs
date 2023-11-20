// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using System;

    /// <summary>
    /// This exception is thrown when a resource access failed to authorized.
    /// </summary>
    public class ResourceUnauthorizedException : UnauthorizedAccessException
    {
        /// <inheritdoc />
        public ResourceUnauthorizedException()
        {
        }

        /// <inheritdoc />
        public ResourceUnauthorizedException(string message) :
            base(message)
        {
        }

        /// <inheritdoc />
        public ResourceUnauthorizedException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }
}
