﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when serializer fails.
    /// </summary>
    public class SerializerException : Exception
    {
        /// <inheritdoc />
        public SerializerException() :
            this("Failed to serializer or deserialize")
        {
        }

        /// <inheritdoc />
        public SerializerException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public SerializerException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }
}
