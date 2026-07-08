// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore
{
    /// <summary>
    /// Common runtime environment variables for AspNetCore
    /// configuration.
    /// </summary>
    public static class EnvironmentVariable
    {
        /// <summary> Enable processing of forwarded headers </summary>
        public const string FORWARDEDHEADERSENABLED =
            "ASPNETCORE_FORWARDEDHEADERS_ENABLED";
        /// <summary> Limit number of entries in the forwarded headers. </summary>
        public const string FORWARDEDHEADERSFORWARDLIMIT =
            "ASPNETCORE_FORWARDEDHEADERS_FORWARDLIMIT";
        /// <summary>
        /// Comma/semicolon separated list of trusted proxy IP addresses
        /// whose forwarded headers are honored.
        /// </summary>
        public const string FORWARDEDHEADERSKNOWNPROXIES =
            "ASPNETCORE_FORWARDEDHEADERS_KNOWNPROXIES";
        /// <summary>
        /// Comma/semicolon separated list of trusted proxy networks in CIDR
        /// notation (e.g. 10.0.0.0/8) whose forwarded headers are honored.
        /// </summary>
        public const string FORWARDEDHEADERSKNOWNNETWORKS =
            "ASPNETCORE_FORWARDEDHEADERS_KNOWNNETWORKS";
        /// <summary>
        /// Opt-in to trust forwarded headers from any remote address. This
        /// disables known-proxy validation and should only be used when the
        /// app is not directly reachable by untrusted clients.
        /// </summary>
        public const string FORWARDEDHEADERSTRUSTALLPROXIES =
            "ASPNETCORE_FORWARDEDHEADERS_TRUSTALLPROXIES";
        /// <summary> Redirect port </summary>
        public const string HTTPSREDIRECTPORT =
            "ASPNETCORE_HTTPSREDIRECTPORT";
        /// <summary> Whether openapi should be enabled (Swagger) </summary>
        public const string OPENAPIENABLED =
            "ASPNETCORE_OPENAPI_ENABLED";
        /// <summary> Whether create v2 openapi json </summary>
        public const string OPENAPIUSEV2 =
            "ASPNETCORE_OPENAPI_USE_V2";
        /// <summary> Server host for openapi </summary>
        public const string OPENAPISERVERHOST =
            "ASPNETCORE_OPENAPI_SERVER_HOST";
    }
}
