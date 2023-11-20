// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Net.Http;

    public static class HttpTunnelFixture
    {
        public static IHttpClientFactory CreateHttpClientFactory(HttpClientHandler? handler = null)
        {
            var services = new ServiceCollection();
            var builder = services.AddHttpClient().AddHttpClient("msft");
            if (handler != null)
            {
                builder.ConfigurePrimaryHttpMessageHandler(() => handler);
            }
            services.AddLogging();
            return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        }
    }
}
