// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using Furly.Exceptions;
    using System;

    /// <summary>
    /// Host has not been initialized
    /// </summary>
    public class NotInitializedException : ResourceInvalidStateException
    {
        /// <inheritdoc/>
        public NotInitializedException() :
            this("Please call 'Initialize' method before running.")
        {
        }

        /// <inheritdoc/>
        public NotInitializedException(string message) : base(message)
        {
        }

        /// <inheritdoc/>
        public NotInitializedException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }
}
