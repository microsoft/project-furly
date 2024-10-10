// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Rpc.Runtime;
    using Furly.Extensions.Rpc.Servers;
    using System;

    /// <summary>
    /// Database
    /// </summary>
    public static class RpcContainerBuilderEx
    {
        /// <summary>
        /// Add server
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddFileSystemServer(this ContainerBuilder builder)
        {
            builder.RegisterType<FileSystemRpcServer>()
                .AsSelf()
                .AsImplementedInterfaces().SingleInstance();
            return builder;
        }
    }
}
