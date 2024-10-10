// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Rpc.Runtime;
    using Furly.Extensions.Rpc.Servers;
    using System;

    /// <summary>
    /// Service collection extensions
    /// </summary>
    public static class RpcServiceCollectionEx
    {
        /// <summary>
        /// Add file system Rpc client
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configureOptions"></param>
        public static IServiceCollection AddFileSystemRpcServer(this IServiceCollection services,
            Action<FileSystemRpcServerOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            return services
                .AddScoped<IRpcServer, FileSystemRpcServer>()
                ;
        }
    }
}
