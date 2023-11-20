// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.Web;

    /// <summary>
    /// Uri extensions
    /// </summary>
    public static class UriEx
    {
        /// <summary>
        /// Returns a query and fragmentless uri
        /// </summary>
        /// <param name="uri"></param>
        public static Uri NoQueryAndFragment(this Uri uri)
        {
            return new UriBuilder(uri) { Fragment = null, Query = null }.Uri;
        }

        /// <summary>
        /// Replace host name with the one in the discovery url
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="host"></param>
        public static Uri ChangeHost(this Uri uri, string host)
        {
            return new UriBuilder(uri) { Host = host }.Uri;
        }

        /// <summary>
        /// Encode a string for inclusion in url
        /// </summary>
        /// <param name="value"></param>
#pragma warning disable CA1055 // URI-like return values should not be strings
        public static string UrlEncode(this string value)
#pragma warning restore CA1055 // URI-like return values should not be strings
        {
            return HttpUtility.UrlEncode(value);
        }

        /// <summary>
        /// Decode a string
        /// </summary>
        /// <param name="value"></param>
#pragma warning disable CA1055 // URI-like return values should not be strings
        public static string UrlDecode(this string value)
#pragma warning restore CA1055 // URI-like return values should not be strings
        {
            return HttpUtility.UrlDecode(value);
        }
    }
}
