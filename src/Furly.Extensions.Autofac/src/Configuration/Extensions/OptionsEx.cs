// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;
    using System;

    /// <summary>
    /// Container builder Options extensions
    /// </summary>
    public static class OptionsEx
    {
        /// <summary>
        /// Configure options
        /// </summary>
        /// <typeparam name="TOptions"></typeparam>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        public static ContainerBuilder Configure<TOptions>(this ContainerBuilder builder,
            Action<TOptions> configure) where TOptions : class
        {
            builder.RegisterInstance(new ConfigureOptions<TOptions>(configure))
                .AsImplementedInterfaces();
            return builder.AddOptions();
        }

#if UNUSED
        /// <summary>
        /// Configure options
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static ContainerBuilder Configure<TOptions>(this ContainerBuilder builder,
            string name, Action<TOptions> configure) where TOptions : class {
            builder.AddOptions();
            builder.RegisterInstance(new ConfigureNamedOptions<TOptions>(name, configure))
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Post configure options
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static ContainerBuilder PostConfigure<TOptions>(this ContainerBuilder builder,
            string name, Action<TOptions> configure) where TOptions : class {
            builder.AddOptions();
            builder.RegisterInstance(new PostConfigureOptions<TOptions>(name, configure))
                .AsImplementedInterfaces();
            return builder;
        }

        /// <summary>
        /// Validate options
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="validation"></param>
        /// <param name="name"></param>
        /// <param name="failureMessage"></param>
        /// <returns></returns>
        public static ContainerBuilder Validate<TOptions>(this ContainerBuilder builder,
            string name, Func<TOptions, bool> validation, string failureMessage = null)
                where TOptions : class {
            failureMessage ??= $"Failed to validate {name} of type {typeof(TOptions)}";
            builder.AddOptions();
            builder.RegisterInstance(new ValidateOptions<TOptions>(name, validation, failureMessage))
                .AsImplementedInterfaces();
            return builder;
        }
#endif

        /// <summary>
        /// Add options to container
        /// </summary>
        /// <param name="builder"></param>
        public static ContainerBuilder AddOptions(this ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(OptionsManager<>))
                .SingleInstance()
                .As(typeof(IOptions<>));
            builder.RegisterGeneric(typeof(OptionsManager<>))
                .InstancePerLifetimeScope()
                .As(typeof(IOptionsSnapshot<>));
            builder.RegisterGeneric(typeof(OptionsMonitor<>))
                .SingleInstance()
                .As(typeof(IOptionsMonitor<>));
            builder.RegisterGeneric(typeof(OptionsFactory<>))
                .InstancePerDependency()
                .As(typeof(IOptionsFactory<>));
            builder.RegisterGeneric(typeof(OptionsCache<>))
                .SingleInstance()
                .As(typeof(IOptionsMonitorCache<>));
            builder.RegisterGeneric(typeof(OptionsBinding<>))
                .SingleInstance()
                .AsImplementedInterfaces();

            return builder.AddConfiguration();
        }

        /// <inheritdoc/>
        private class OptionsBinding<TOptions> : IConfigureOptions<TOptions>,
            IConfigureNamedOptions<TOptions>, IOptionsChangeTokenSource<TOptions>
            where TOptions : class
        {
            /// <inheritdoc/>
            public string Name => Options.DefaultName;

            /// <inheritdoc/>
            public OptionsBinding(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            /// <inheritdoc/>
            public void Configure(TOptions options)
            {
                Configure(Options.DefaultName, options);
            }

            /// <inheritdoc/>
            public void Configure(string? name, TOptions options)
            {
                var configuration = _configuration;
                if (configuration != null && name != null && name != Options.DefaultName)
                {
                    // Name is key
                    configuration = configuration.GetSection(name);
                }
                configuration?.Bind(options);
            }

            /// <inheritdoc/>
            public IChangeToken GetChangeToken()
            {
                if (_configuration == null)
                {
                    return kDummyToken.Value;
                }
                return _configuration.GetReloadToken();
            }

            private static readonly Lazy<IChangeToken> kDummyToken =
                new(() =>
                    new ConfigurationBuilder().Build().GetReloadToken());
            private readonly IConfiguration _configuration;
        }
    }
}
