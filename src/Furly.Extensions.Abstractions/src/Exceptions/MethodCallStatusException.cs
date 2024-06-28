// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Exceptions
{
    using Furly.Extensions.Serializers;
    using System;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// This exception is thrown when method call returned a
    /// status other than 200
    /// </summary>
    public class MethodCallStatusException : MethodCallException
    {
        /// <summary>
        /// Problem details
        /// </summary>
        public ErrorDetails Details { get; }

        /// <summary>
        /// Status code
        /// </summary>
        public int Status => Details.Status ?? 500;

        /// <inheritdoc/>
        internal MethodCallStatusException() :
            this((string?)null)
        {
        }

        /// <inheritdoc/>
        public MethodCallStatusException(string? message) :
            this(null, message)
        {
        }

        /// <inheritdoc/>
        public MethodCallStatusException(string? message, Exception innerException) :
            this(null, innerException, message)
        {
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="status"></param>
        /// <param name="errorDetails"></param>
        /// <param name="title"></param>
        /// <param name="type"></param>
        public MethodCallStatusException(int? status, string? errorDetails,
            string? title = null, string? type = null) :
            this(new ErrorDetails
            {
                Detail = errorDetails,
                Title = title,
                Type = type,
                Status = status ?? 500
            })
        {
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="status"></param>
        /// <param name="innerException"></param>
        /// <param name="errorDetails"></param>
        /// <param name="title"></param>
        /// <param name="type"></param>
        public MethodCallStatusException(int? status, Exception innerException,
            string? errorDetails, string? title = null, string? type = null) :
            this(new ErrorDetails
            {
                Detail = errorDetails,
                Title = title,
                Type = type,
                Status = status ?? 500
            }, innerException)
        {
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="details"></param>
        public MethodCallStatusException(ErrorDetails details) :
            base(AsString(details))
        {
            Details = details;
        }

        /// <summary>
        /// Create exception
        /// </summary>
        /// <param name="details"></param>
        /// <param name="innerException"></param>
        public MethodCallStatusException(ErrorDetails details,
            Exception innerException) :
            base(AsString(details), innerException)
        {
            Details = details;
        }

        /// <summary>
        /// Try deserialize exception
        /// </summary>
        /// <param name="response"></param>
        /// <param name="serializer"></param>
        /// <param name="outerStatus"></param>
        public static MethodCallStatusException Deserialize(
            ReadOnlyMemory<byte> response, ISerializer? serializer = null,
            int? outerStatus = null)
        {
            var result = Deserialize(response, serializer,
                outerStatus, out var innerException);
            if (result != null)
            {
                return result;
            }
            var message = Encoding.UTF8.GetString(response.Span);
            if (innerException != null)
            {
                return new MethodCallStatusException(outerStatus ?? 500,
                    innerException, message);
            }
            return new MethodCallStatusException(outerStatus ?? 500, message);
        }

        /// <summary>
        /// Throw
        /// </summary>
        /// <param name="response"></param>
        /// <param name="serializer"></param>
        /// <param name="outerStatus"></param>
        public static void TryThrow(ReadOnlyMemory<byte> response,
            ISerializer? serializer = null, int? outerStatus = null)
        {
            var result = Deserialize(response, serializer, outerStatus, out _);
            if (result != null)
            {
                throw result;
            }
        }

        /// <summary>
        /// Get payload
        /// </summary>
        public ReadOnlyMemory<byte> Serialize(ISerializer? serializer = null)
        {
            if (serializer != null)
            {
                return serializer.SerializeObjectToMemory(Details);
            }
            return JsonSerializer.SerializeToUtf8Bytes(Details).AsMemory();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return AsString(Details);
        }

        /// <summary>
        /// Convert to string message
        /// </summary>
        /// <param name="details"></param>
        /// <returns></returns>
        private static string AsString(ErrorDetails details)
        {
            return JsonSerializer.Serialize(details);
        }

        /// <summary>
        /// Helper to deserialize the payload
        /// </summary>
        /// <param name="response"></param>
        /// <param name="serializer"></param>
        /// <param name="outerStatus"></param>
        /// <param name="innerException"></param>
        /// <returns></returns>
        private static MethodCallStatusException? Deserialize(
            ReadOnlyMemory<byte> response, ISerializer? serializer,
            int? outerStatus, out Exception? innerException)
        {
            innerException = null;
            if (response.Length == 0)
            {
                return new MethodCallStatusException(outerStatus ?? 500, string.Empty);
            }
            if (serializer != null)
            {
                try
                {
                    var details = serializer.Deserialize<ErrorDetails>(response);
                    if (details != null)
                    {
                        details.Status ??= outerStatus;
                        return new MethodCallStatusException(details);
                    }
                }
                catch (Exception ex)
                {
                    innerException = ex;
                }
            }
            try
            {
                var details = JsonSerializer.Deserialize<ErrorDetails>(response.Span);
                if (details != null)
                {
                    details.Status ??= outerStatus;
                    if (innerException != null)
                    {
                        return new MethodCallStatusException(details, innerException);
                    }
                    return new MethodCallStatusException(details);
                }
            }
            catch (Exception ex)
            {
                innerException = innerException != null ?
                    new AggregateException(ex, innerException) : ex;
            }
            return null;
        }
    }
}
