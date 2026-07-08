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
    using System;
    using System.Collections.Generic;
    using System.Net;
    using IPNetwork = System.Net.IPNetwork;

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

            // Register explicitly trusted proxies / networks so that forwarded
            // headers are only honored when they originate from them. This keeps
            // ASP.NET Core's known-proxy validation active (the secure default).
            var restricted = AddKnownProxies(options);
            restricted |= AddKnownNetworks(options);

            // Only disable known-proxy validation (i.e. trust forwarded headers
            // from any remote address) when the operator explicitly opts in and
            // has not already narrowed trust via the lists above. Without this
            // opt-in the framework default (loopback only) is preserved, which
            // prevents arbitrary clients from spoofing X-Forwarded-For /
            // X-Forwarded-Proto.
            if (!restricted && GetBoolOrDefault(
                EnvironmentVariable.FORWARDEDHEADERSTRUSTALLPROXIES))
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        }

        /// <inheritdoc/>
        public void Configure(ForwardedHeadersOptions options)
        {
            Configure(Options.DefaultName, options);
        }

        /// <summary>
        /// Add configured trusted proxy addresses to the known proxies.
        /// </summary>
        /// <param name="options"></param>
        /// <returns>True if at least one proxy was added.</returns>
        private bool AddKnownProxies(ForwardedHeadersOptions options)
        {
            var added = false;
            foreach (var entry in Split(GetStringOrDefault(
                EnvironmentVariable.FORWARDEDHEADERSKNOWNPROXIES)))
            {
                if (IPAddress.TryParse(entry, out var address))
                {
                    options.KnownProxies.Add(address);
                    added = true;
                }
            }
            return added;
        }

        /// <summary>
        /// Add configured trusted proxy networks to the known networks.
        /// </summary>
        /// <param name="options"></param>
        /// <returns>True if at least one network was added.</returns>
        private bool AddKnownNetworks(ForwardedHeadersOptions options)
        {
            var added = false;
            foreach (var entry in Split(GetStringOrDefault(
                EnvironmentVariable.FORWARDEDHEADERSKNOWNNETWORKS)))
            {
                if (IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                    added = true;
                }
            }
            return added;
        }

        /// <summary>
        /// Split a comma/semicolon/space separated list into entries.
        /// </summary>
        /// <param name="value"></param>
        private static IEnumerable<string> Split(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }
            foreach (var part in value.Split([',', ';', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }
    }
}
