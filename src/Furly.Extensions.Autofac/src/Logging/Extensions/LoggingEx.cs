// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Autofac.Extensions.DependencyInjection;
    using Furly.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;

    /// <summary>
    /// Container builder Logging extensions
    /// </summary>
    public static class LoggingEx
    {
        /// <summary>
        /// Register default diagnostics
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        public static ContainerBuilder AddLogging(this ContainerBuilder builder,
            Action<ILoggingBuilder>? configure = null)
        {
            builder.RegisterType<HealthCheckRegistrar>()
                .AsImplementedInterfaces().SingleInstance();

            // Add logging
            builder.AddOptions();
            builder.RegisterModule<LoggingModule>();

            var log = new LogBuilder();
            configure?.Invoke(log);

            builder.Populate(log.Services);
            return builder;
        }

        /// <summary>
        /// Log builder adapter
        /// </summary>
        private class LogBuilder : ILoggingBuilder
        {
            /// <inheritdoc/>
            public IServiceCollection Services { get; }
                = new ServiceCollection();
        }
    }
}
