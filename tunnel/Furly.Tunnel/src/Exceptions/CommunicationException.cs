// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using System;
    using System.IO;

    /// <summary>
    /// Thrown when failing to connect to resource
    /// </summary>
    public class CommunicationException : IOException
    {
        /// <inheritdoc />
        public CommunicationException(string message) :
            base(message)
        {
        }

        /// <inheritdoc />
        public CommunicationException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        /// <inheritdoc />
        public CommunicationException()
        {
        }
    }
}
