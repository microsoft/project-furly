// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Server
{
    using Asp.Versioning;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Webservice startup
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Current hosting environment - Initialized in constructor
        /// </summary>
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Create startup
        /// </summary>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Environment = env;
            Configuration = configuration;
        }

        /// <summary>
        /// This is where you register dependencies, add services to the
        /// container. This method is called by the runtime, before the
        /// Configure method below.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(o => o.AddConsole().AddDebug());

            services.AddHeaderForwarding();
            services.AddCors();
            services.AddHealthChecks();
            services.AddHttpsRedirect();
            services.AddHttpClient();

            services.AddApiVersioning(o =>
            {
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0);
            }).AddMvc();

            // Add controllers as services so they'll be resolved.
            services.AddControllers();
            services.AddHttpTunnel();
            services.AddDefaultJsonSerializer();
        }

        /// <summary>
        /// This method is called by the runtime, after the ConfigureServices
        /// method above and used to add middleware
        /// </summary>
        /// <param name="app"></param>
        /// <param name="appLifetime"></param>
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime appLifetime)
        {
            var applicationContainer = app.ApplicationServices.GetAutofacRoot();

            app.UsePathBase();
            app.UseHeaderForwarding();

            app.UseRouting();
            app.UseHttpsRedirect();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/healthz");
            });

            // Connect application servers
            app.UseHttpTunnel();

            // If you want to dispose of resources that have been resolved in the
            // application container, register for the "ApplicationStopped" event.
            appLifetime.ApplicationStopped.Register(applicationContainer.Dispose);
        }

        /// <summary>
        /// Autofac configuration.
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            // This should be a real rpc server
            builder.RegisterType<InMemoryRpcServer>()
                .AsImplementedInterfaces().SingleInstance();
            // This should be a real broker
            builder.RegisterType<InMemoryEventBroker>()
                .AsImplementedInterfaces().SingleInstance();
        }
    }
}
