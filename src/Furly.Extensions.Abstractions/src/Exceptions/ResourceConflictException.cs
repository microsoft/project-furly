// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using System;

    /// <summary>
    /// This exception is thrown when a client attempts to create a resource
    /// which would conflict with an existing one, for instance using the same
    /// identifier. The client should change the identifier or assume the
    /// resource has already been created.
    /// </summary>
    public class ResourceConflictException : Exception
    {
        /// <inheritdoc />
        public ResourceConflictException()
        {
        }

        /// <inheritdoc />
        public ResourceConflictException(string message) :
            base(message)
        {
        }

        /// <inheritdoc />
        public ResourceConflictException(string message, Exception? innerException) :
            base(message, innerException)
        {
        }
    }
}
