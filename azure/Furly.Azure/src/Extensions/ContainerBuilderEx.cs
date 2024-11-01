// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Azure;

    /// <summary>
    /// Container builder extensions
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <summary>
        /// Add default azure credentials
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddDefaultAzureCredentials(this ContainerBuilder builder)
        {
            builder.AddOptions();
            builder.RegisterType<DefaultAzureCredentials>()
                .AsImplementedInterfaces().InstancePerLifetimeScope()
                .IfNotRegistered(typeof(ICredentialProvider));
            return builder;
        }
    }
}
