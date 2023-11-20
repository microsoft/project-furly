// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Http
{
    using Furly.Extensions.Http;
    using System;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Http request extensions
    /// </summary>
    public static class HttpRequestEx
    {
        /// <summary>
        /// Get page size from header
        /// </summary>
        /// <param name="request"></param>
        /// <param name="pageSize"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static int? GetPageSize(this HttpRequest request, int? pageSize = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (pageSize == null && request.Headers.TryGetValue(HttpHeader.MaxItemCount, out var value))
            {
                var count = value.FirstOrDefault();
                if (count != null)
                {
                    return int.Parse(count, CultureInfo.InvariantCulture);
                }
            }
            return pageSize;
        }

        /// <summary>
        /// Get page size from header
        /// </summary>
        /// <param name="request"></param>
        /// <param name="continuationToken"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static string? GetContinuationToken(this HttpRequest request,
            string? continuationToken = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrEmpty(continuationToken) &&
                request.Headers.TryGetValue(HttpHeader.ContinuationToken, out var value))
            {
                continuationToken = value.FirstOrDefault();
            }
            return continuationToken;
        }
    }
}
