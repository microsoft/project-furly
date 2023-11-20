// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.IoT.Service
{
    using Furly.Tunnel.Azure.IoT.Service.Runtime;
    using Furly.Tunnel.Services;
    using Furly.Azure.IoT;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// IoT Hub cloud proxy. Processes all tunnel requests from devices.
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
                    services.AddIoTHubRpcClient();
                    services.AddIoTHubEventSubscriber();
                    services.Configure<IoTHubEventProcessorOptions>(options =>
                        options.ConsumerGroup = "tunnel");
                    services.AddHostedService<CloudProxy>();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((context, builder) =>
                {
                    // Handle tunnel server events
                    builder.RegisterType<HttpTunnelHybridServer>().AsSelf()
                        .AsImplementedInterfaces();
                    builder.AddConfiguration(context.Configuration);
                    builder.RegisterType<ServiceInfo>()
                        .AsImplementedInterfaces();
                });
        }
    }
}
