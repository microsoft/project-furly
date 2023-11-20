// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Hosting.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Forwarded headers processing configuration.
    /// </summary>
    internal sealed class HeadersConfig : PostConfigureOptionBase<HeadersOptions>,
        IConfigureOptions<ForwardedHeadersOptions>,
        IConfigureNamedOptions<ForwardedHeadersOptions>
    {
        /// <inheritdoc/>
        public HeadersConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, HeadersOptions options)
        {
            if (!options.ForwardingEnabled)
            {
                options.ForwardingEnabled = GetBoolOrDefault(
                    EnvironmentVariable.FORWARDEDHEADERSENABLED);
            }
        }

        /// <inheritdoc/>
        public void Configure(string? name, ForwardedHeadersOptions options)
        {
            options.ForwardLimit = GetIntOrNull(
                EnvironmentVariable.FORWARDEDHEADERSFORWARDLIMIT,
                    options.ForwardLimit);
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            // Only loopback proxies are allowed by default.
            // Clear that restriction because forwarders are enabled by explicit
            // configuration.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }

        /// <inheritdoc/>
        public void Configure(ForwardedHeadersOptions options)
        {
            Configure(Options.DefaultName, options);
        }
    }
}
