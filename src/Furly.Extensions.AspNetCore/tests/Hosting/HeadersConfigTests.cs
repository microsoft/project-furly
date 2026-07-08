// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Tests.Hosting
{
    using Furly.Extensions.AspNetCore.Hosting.Runtime;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.Extensions.Configuration;
    using System.Collections.Generic;
    using System.Net;
    using Xunit;
    using IPNetwork = System.Net.IPNetwork;

    public class HeadersConfigTests
    {
        [Fact]
        public void DefaultDoesNotDisableKnownProxyValidation()
        {
            var options = new ForwardedHeadersOptions();
            var defaultProxies = options.KnownProxies.Count;
            var defaultNetworks = options.KnownIPNetworks.Count;

            CreateConfig().Configure(options);

            // The loopback defaults must be preserved so forwarded headers are
            // only trusted from known proxies (secure default).
            Assert.Equal(defaultProxies, options.KnownProxies.Count);
            Assert.Equal(defaultNetworks, options.KnownIPNetworks.Count);
            Assert.True(options.KnownProxies.Count > 0 || options.KnownIPNetworks.Count > 0);
            Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                options.ForwardedHeaders);
        }

        [Fact]
        public void ConfiguredKnownProxiesAreAdded()
        {
            var options = new ForwardedHeadersOptions();
            CreateConfig(new Dictionary<string, string?>
            {
                [EnvironmentVariable.FORWARDEDHEADERSKNOWNPROXIES] = "10.1.2.3, 2001:db8::1"
            }).Configure(options);

            Assert.Contains(IPAddress.Parse("10.1.2.3"), options.KnownProxies);
            Assert.Contains(IPAddress.Parse("2001:db8::1"), options.KnownProxies);
        }

        [Fact]
        public void ConfiguredKnownNetworksAreAdded()
        {
            var options = new ForwardedHeadersOptions();
            CreateConfig(new Dictionary<string, string?>
            {
                [EnvironmentVariable.FORWARDEDHEADERSKNOWNNETWORKS] = "10.0.0.0/8;192.168.0.0/16"
            }).Configure(options);

            Assert.Contains(IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
            Assert.Contains(IPNetwork.Parse("192.168.0.0/16"), options.KnownIPNetworks);
        }

        [Fact]
        public void TrustAllOptInClearsKnownProxiesAndNetworks()
        {
            var options = new ForwardedHeadersOptions();
            CreateConfig(new Dictionary<string, string?>
            {
                [EnvironmentVariable.FORWARDEDHEADERSTRUSTALLPROXIES] = "true"
            }).Configure(options);

            Assert.Empty(options.KnownProxies);
            Assert.Empty(options.KnownIPNetworks);
        }

        [Fact]
        public void TrustAllOptInIsIgnoredWhenProxiesConfigured()
        {
            var options = new ForwardedHeadersOptions();
            CreateConfig(new Dictionary<string, string?>
            {
                [EnvironmentVariable.FORWARDEDHEADERSTRUSTALLPROXIES] = "true",
                [EnvironmentVariable.FORWARDEDHEADERSKNOWNPROXIES] = "10.1.2.3"
            }).Configure(options);

            // Explicit trust must win over the trust-all opt-in.
            Assert.Contains(IPAddress.Parse("10.1.2.3"), options.KnownProxies);
            Assert.NotEmpty(options.KnownProxies);
        }

        private static HeadersConfig CreateConfig(IDictionary<string, string?>? values = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
                .Build();
            return new HeadersConfig(configuration);
        }
    }
}
