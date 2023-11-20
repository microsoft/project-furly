// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Logging
{
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Activators.Reflection;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Debug;
    using System;
    using System.Linq;

    /// <summary>
    /// Log module
    /// </summary>
    public class LoggingModule : Module
    {
        /// <inheritdoc/>
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(Logger<>))
                .SingleInstance()
                .As(typeof(ILogger<>));
            builder.RegisterType(typeof(Logger<LoggingModule>)) // Root logger
                .SingleInstance()
                .As(typeof(ILogger));
            builder.RegisterType<LoggerFactory>()
                .As<ILoggerFactory>()
                .SingleInstance()
                .IfNotRegistered(typeof(ILoggerFactory));
            builder.RegisterType<DebugLoggerProvider>()
                .IfNotRegistered(typeof(DebugLoggerProvider))
                .As<DebugLoggerProvider>()
                .As<ILoggerProvider>()
                .SingleInstance();
            base.Load(builder);
        }

        /// <inheritdoc/>
        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry,
            IComponentRegistration registration)
        {
            if (registration.Activator is ReflectionActivator ra)
            {
                try
                {
                    var ctors = ra.ConstructorFinder.FindConstructors(ra.LimitType);
                    // Only inject logger in components with a ILogger dependency
                    var usesLogger = ctors
                        .SelectMany(ctor => ctor.GetParameters())
                        .Any(pi => pi.ParameterType == typeof(ILogger));
                    if (usesLogger)
                    {
                        // Attach updater
                        registration.PipelineBuilding += (_, pipeline) =>
                           pipeline.Use(new LoggerInjector(registration.Activator.LimitType));
                    }
                }
                catch (NoConstructorsFoundException)
                {
                }
            }
        }

        private class LoggerInjector : IResolveMiddleware
        {
            /// <inheritdoc/>
            public PipelinePhase Phase => PipelinePhase.ParameterSelection;

            public LoggerInjector(Type type)
            {
                _type = type;
            }

            /// <inheritdoc/>
            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                var type = typeof(ILogger<>).MakeGenericType(_type);
                var logger = (ILogger)context.Resolve(type);
                context.ChangeParameters(new[] { TypedParameter.From(logger) }
                    .Concat(context.Parameters));
                // Continue the resolve.
                next(context);
            }

            private readonly Type _type;
        }
    }
}
