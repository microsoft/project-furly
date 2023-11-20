// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when method call failed
    /// </summary>
    public class MethodCallException : Exception
    {
        /// <inheritdoc/>
        public MethodCallException(string message) :
            base(message)
        {
        }

        /// <inheritdoc/>
        public MethodCallException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        /// <inheritdoc/>
        public MethodCallException()
        {
        }
    }
}
