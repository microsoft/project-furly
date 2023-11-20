// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests
{
    using Furly.Tunnel.AspNetCore.Tests.Server;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers.Json;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <inheritdoc/>
    public class InMemoryServerFixture : WebApplicationFactory<Startup>
    {
        /// <summary>
        /// Helper to test multiple serializers in theories
        /// </summary>
        /// <returns></returns>
#pragma warning disable CA1024 // Use properties where appropriate
        public static IEnumerable<object[]> GetSerializers()
#pragma warning restore CA1024 // Use properties where appropriate
        {
            yield return new object[] { new DefaultJsonSerializer() };
        }

        /// <inheritdoc/>
        protected override IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder();
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(".")
                .UseStartup<Startup>();
            base.ConfigureWebHost(builder);
        }

        /// <inheritdoc/>
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            return base.CreateHost(builder);
        }

        /// <summary>
        /// Resolve service
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Resolve<T>()
        {
            return (T)Server.Services.GetRequiredService(typeof(T));
        }

        public HttpClient GetHttpClientWithTunnelOverMethodClient()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddHttpClientWithTunnelOverMethodClient(string.Empty);
            services.AddSingleton(Resolve<IRpcClient>());
            return services.BuildServiceProvider()
                .GetRequiredService<IHttpClientFactory>().CreateClient(string.Empty);
        }

        public HttpClient GetHttpClientWithTunnelOverEventClient()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddHttpClientWithTunnelOverEventClient(string.Empty);
            services.AddSingleton(Resolve<IEventClient>());
            services.AddSingleton(Resolve<IEventSubscriber>());
            return services.BuildServiceProvider()
                .GetRequiredService<IHttpClientFactory>().CreateClient(string.Empty);
        }
    }
}
