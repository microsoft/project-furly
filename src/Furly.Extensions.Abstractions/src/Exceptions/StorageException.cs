// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when accessing storage systems fails.
    /// </summary>
    public class StorageException : ExternalDependencyException
    {
        /// <inheritdoc />
        public StorageException() :
            this("Failed a storage operation")
        {
        }

        /// <inheritdoc />
        public StorageException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public StorageException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }
}
