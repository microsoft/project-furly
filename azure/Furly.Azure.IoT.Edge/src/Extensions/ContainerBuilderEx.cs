// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Azure.IoT.Edge.Runtime;
    using Furly.Azure.IoT.Edge.Services;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add IoT Edge hosting services
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddIoTEdgeServices(this ContainerBuilder builder)
        {
            // Clients
            builder.RegisterType<IoTEdgeClientConfig>()
                .AsImplementedInterfaces();

            builder.RegisterType<IoTEdgeIdentity>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<IoTEdgeHubSdkClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<IoTEdgeWorkloadApi>()
                .AsImplementedInterfaces();

            builder.RegisterType<IoTEdgeEventClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<IoTEdgeTwinClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<IoTEdgeRpcClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<IoTEdgeRpcServer>()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }
    }
}
