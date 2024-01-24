// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Tests.Fixtures
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Net.Http;

    /// <summary>
    /// Create web applications
    /// </summary>
    public static class WebApp
    {
        public static WebAppFixture<TStartup> Create<TStartup>(ILoggerFactory loggerFactory)
            where TStartup : class
        {
            return new WebAppFixture<TStartup>(loggerFactory);
        }
    }

    /// <summary>
    /// Fixture to run web application for testing
    /// </summary>
    /// <typeparam name="TStartup"></typeparam>
    public class WebAppFixture<TStartup> : WebApplicationFactory<TStartup>, IHttpClientFactory
        where TStartup : class
    {
        public WebAppFixture()
        {
            _loggerFactory = null;
        }

        internal WebAppFixture(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        protected override IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder();
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder
                .UseContentRoot(".")
                .UseStartup<TStartup>()
                .ConfigureServices(services =>
                {
                    if (_loggerFactory != null)
                    {
                        services.AddSingleton(_loggerFactory);
                    }
                    services.AddMvc()
                        .AddControllersAsServices();
                })
                ;
            base.ConfigureWebHost(builder);
        }

        /// <inheritdoc/>
        public HttpClient CreateClient(string name)
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        private readonly ILoggerFactory? _loggerFactory;
    }
}
