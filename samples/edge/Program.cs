// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.IoT.Edge.Service
{
    using Furly.Tunnel.Azure.IoT.Edge.Service.Runtime;
    using Furly.Tunnel.Services;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Edge proxy: Process method request and forward to http endpoint
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point for iot hub device event processor host
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Create host builder
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(builder => builder
                    .AddFromDotEnvFile()
                    .AddEnvironmentVariables())
                .ConfigureServices((_, services) =>
                {
                    services.AddHealthChecks();
                    services.AddHttpClient();
                    services.AddDefaultJsonSerializer();
                    services.AddIoTEdgeServices();
                    services.AddHostedService<EdgeProxy>();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((context, builder) =>
                {
                    // Handle tunnel server events
                    builder.RegisterType<HttpTunnelMethodServer>().AsSelf()
                        .AsImplementedInterfaces();
                    builder.AddConfiguration(context.Configuration);
                    builder.RegisterType<ServiceInfo>()
                        .AsImplementedInterfaces();
                });
        }
    }
}
