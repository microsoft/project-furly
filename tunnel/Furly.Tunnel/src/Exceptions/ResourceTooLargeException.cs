// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when a resource does not fit into the allowed
    /// storage allocation
    /// </summary>
    public class ResourceTooLargeException : Exception
    {
        /// <summary>
        /// Actual size
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Max allowed size
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="size"></param>
        /// <param name="maxSize"></param>
        public ResourceTooLargeException(string message,
            int size, int maxSize) : base(message)
        {
            Size = size;
            MaxSize = maxSize;
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="size"></param>
        /// <param name="maxSize"></param>
        /// <param name="innerException"></param>
        public ResourceTooLargeException(string message,
            int size, int maxSize, Exception innerException) :
            base(message, innerException)
        {
            Size = size;
            MaxSize = maxSize;
        }

        /// <inheritdoc />
        public ResourceTooLargeException(string message) :
            this(message, -1, -1)
        {
        }

        /// <inheritdoc />
        public ResourceTooLargeException(string message,
            Exception innerException) :
            this(message, -1, -1, innerException)
        {
        }

        /// <inheritdoc />
        public ResourceTooLargeException() :
            this("Too large")
        {
        }
    }
}
