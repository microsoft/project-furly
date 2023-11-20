// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Autofac.Core.Resolving.Pipeline;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Configuration.EnvironmentVariables;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Autofac builder configuration extensions
    /// </summary>
    public static class ConfigurationEx
    {
        /// <summary>
        /// Add configuration
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="priority"></param>
        public static ContainerBuilder AddConfiguration(this ContainerBuilder builder,
            IConfiguration configuration, ConfigSourcePriority priority = ConfigSourcePriority.Normal)
        {
            return builder.AddConfigurationSource(new ChainedConfigurationSource
            {
                Configuration = configuration,
                ShouldDisposeConfiguration = false
            }, priority);
        }

        /// <summary>
        /// Add environment variables
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="priority"></param>
        public static ContainerBuilder AddEnvironmentVariableConfiguration(
            this ContainerBuilder builder, ConfigSourcePriority priority = ConfigSourcePriority.Normal)
        {
            return builder.AddConfigurationSource(new EnvironmentVariablesConfigurationSource(), priority);
        }

        /// <summary>
        /// Add configuration source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="priority"></param>
        public static ContainerBuilder AddConfigurationSource<T>(this ContainerBuilder builder,
            ConfigSourcePriority priority = ConfigSourcePriority.Normal)
            where T : IConfigurationSource, new()
        {
            return builder.AddConfigurationSource(new T(), priority);
        }

        /// <summary>
        /// Add configuration source
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="source"></param>
        /// <param name="priority"></param>
        public static ContainerBuilder AddConfigurationSource(this ContainerBuilder builder,
            IConfigurationSource source, ConfigSourcePriority priority = ConfigSourcePriority.Normal)
        {
            return builder.AddConfigurationSource(
                new ConfigurationBuilderResolver(_ => source), priority);
        }

        /// <summary>
        /// Add configuration source
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <param name="priority"></param>
        public static ContainerBuilder AddConfigurationSource(this ContainerBuilder builder,
            Func<IConfigurationRoot, IConfigurationSource?> configure,
            ConfigSourcePriority priority = ConfigSourcePriority.Normal)
        {
            return builder.AddConfigurationSource(
                new ConfigurationBuilderResolver(builder => configure(builder.Build()),
                    priority == ConfigSourcePriority.Normal), ConfigSourcePriority.Low);
        }

#if UNUSED
        /// <summary>
        /// Add configuration
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static ContainerBuilder AddKeyValueConfigurationSource<T>(this ContainerBuilder builder,
            IEnumerable<KeyValuePair<string, string>> configuration,
            ConfigSourcePriority priority = ConfigSourcePriority.Normal) {
            return builder.AddConfigurationSource(new MemoryConfigurationSource {
                InitialData = configuration
            }, priority);
        }

        /// <summary>
        /// Adds .env file environment variables
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static ContainerBuilder AddDotEnvFileConfiguration(
            this ContainerBuilder builder, ConfigSourcePriority priority = ConfigSourcePriority.Normal) {
            return builder.AddConfigurationSource(new DotEnvFileSource(), priority);
        }
#endif

        /// <summary>
        /// Adds configuration
        /// </summary>
        /// <param name="builder"></param>
        internal static ContainerBuilder AddConfiguration(this ContainerBuilder builder)
        {
            builder.RegisterType<PriorityConfigurationBuilder>().As<IConfigurationBuilder>();
            builder.Register(ctx => ctx.Resolve<IConfigurationBuilder>().Build())
                .As<IConfiguration>().As<IConfigurationRoot>().InstancePerLifetimeScope();
            return builder;
        }

        /// <summary>
        /// Adds configuration source and resolver
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="resolver"></param>
        /// <param name="priority"></param>
        private static ContainerBuilder AddConfigurationSource(this ContainerBuilder builder,
            ConfigurationBuilderResolver resolver, ConfigSourcePriority priority)
        {
            //
            // we use a 2 phase model - we insert low priority at the start
            // and normal at end.  For factories that need to resolve from the
            // previously added sources we add the resolver at the start but
            // allow the resulting source to again be inserted into the
            // priority builder's sources either at the front or end.
            //
            builder.RegisterServiceMiddleware<IConfigurationBuilder>(resolver,
                priority == ConfigSourcePriority.Low ?
                    MiddlewareInsertionMode.StartOfPhase : MiddlewareInsertionMode.EndOfPhase);
            return builder.AddConfiguration();
        }

        /// <summary>
        /// Builds sources in reverse
        /// </summary>
        private sealed class PriorityConfigurationBuilder : IConfigurationBuilder
        {
            /// <inheritdoc/>
            public IDictionary<string, object> Properties { get; }
                = new Dictionary<string, object>();

            /// <inheritdoc/>
            public IList<IConfigurationSource> Sources { get; }
                = new List<IConfigurationSource>();

            /// <inheritdoc/>
            public IConfigurationBuilder Add(IConfigurationSource source)
            {
                Sources.Add(source);
                return this;
            }

            /// <inheritdoc/>
            public IConfigurationRoot Build()
            {
                return new ConfigurationRoot(Sources
                    .Select(c => c)
                    .Reverse()
                    .Select(s => s.Build(this))
                    .ToList());
            }
        }

        /// <summary>
        /// Resolves a configuration source
        /// </summary>
        private sealed class ConfigurationBuilderResolver : IResolveMiddleware
        {
            /// <inheritdoc/>
            public PipelinePhase Phase => PipelinePhase.ResolveRequestStart;

            /// <summary>
            /// Create resolver
            /// </summary>
            /// <param name="factory"></param>
            /// <param name="insertFirst"></param>
            public ConfigurationBuilderResolver(
                Func<IConfigurationBuilder, IConfigurationSource?> factory,
                bool insertFirst = false)
            {
                _factory = factory;
                _insertFirst = insertFirst;
            }

            /// <inheritdoc/>
            public void Execute(ResolveRequestContext context,
                Action<ResolveRequestContext> next)
            {
                next(context);
                // now this should be our reverse builder - if not change that.
                if (context.Instance is not PriorityConfigurationBuilder builder)
                {
                    builder = new PriorityConfigurationBuilder();
                }
                var source = _factory.Invoke(builder);
                if (source != null)
                {
                    if (_insertFirst)
                    {
                        builder.Sources.Insert(0, source);
                    }
                    else
                    {
                        builder.Add(source);
                    }
                }
            }

            private readonly Func<IConfigurationBuilder, IConfigurationSource?> _factory;
            private readonly bool _insertFirst;
        }
    }
}
