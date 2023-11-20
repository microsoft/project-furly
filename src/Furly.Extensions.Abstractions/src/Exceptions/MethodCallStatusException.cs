// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using System;
    using System.Text;

    /// <summary>
    /// This exception is thrown when method call returned a
    /// status other than 200
    /// </summary>
    public class MethodCallStatusException : MethodCallException
    {
        /// <summary>
        /// Result of method call
        /// </summary>
        public int Result { get; }

        /// <summary>
        /// Payload
        /// </summary>
        public ReadOnlyMemory<byte> ResponsePayload { get; }

        /// <summary>
        /// As string
        /// </summary>
        public string ResponseMessage => ToString(ResponsePayload);

        /// <inheritdoc/>
        public MethodCallStatusException() :
            this(500, "")
        {
        }

        /// <inheritdoc/>
        public MethodCallStatusException(string? message) :
            this(500, message ?? string.Empty)
        {
        }

        /// <inheritdoc/>
        public MethodCallStatusException(string? message, Exception innerException) :
            this(500, message, innerException)
        {
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public MethodCallStatusException(int result, string? errorMessage = null) :
            base($"Response {result} {errorMessage ?? ""}")
        {
            Result = result;
            ResponsePayload = Encoding.UTF8.GetBytes(errorMessage ?? "");
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        /// <param name="innerException"></param>
        public MethodCallStatusException(int result, string? errorMessage,
            Exception innerException) :
            base($"Response {result} {errorMessage ?? ""}", innerException)
        {
            Result = result;
            ResponsePayload = Encoding.UTF8.GetBytes(errorMessage ?? "");
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public MethodCallStatusException(ReadOnlyMemory<byte> responsePayload, int result,
            string? errorMessage = null) :
            base($"Response {result} {errorMessage ?? ""}: {ToString(responsePayload)}")
        {
            Result = result;
            ResponsePayload = responsePayload;
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        /// <param name="innerException"></param>
        public MethodCallStatusException(ReadOnlyMemory<byte> responsePayload, int result,
            string? errorMessage, Exception innerException) :
            base($"Response {result} {errorMessage ?? ""}: {ToString(responsePayload)}",
                innerException)
        {
            Result = result;
            ResponsePayload = responsePayload;
        }

        private static string ToString(ReadOnlyMemory<byte> buffer)
        {
            try
            {
                return Encoding.UTF8.GetString(buffer.Span);
            }
            catch
            {
                // TODO hex string?
                return "Unknown response";
            }
        }
    }
}
