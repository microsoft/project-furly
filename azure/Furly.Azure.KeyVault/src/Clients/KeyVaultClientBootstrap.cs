﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.KeyVault
{
    using Furly.Azure.KeyVault.Runtime;
    using Furly.Exceptions;
    using Autofac;
    using global::Azure.Security.KeyVault.Secrets;
    using System;

    /// <summary>
    /// Retrieve a working Keyvault client to bootstrap keyvault
    /// communcation
    /// </summary>
    public sealed class KeyVaultClientBootstrap : IDisposable
    {
        /// <summary>
        /// Get client
        /// </summary>
        public SecretClient Client { get; }

        /// <summary>
        /// Create bootstrap
        /// </summary>
        /// <param name="keyVaultUri"></param>
        /// <param name="allowInteractiveLogon"></param>
        public KeyVaultClientBootstrap(string keyVaultUri,
            bool allowInteractiveLogon = false)
        {
            if (string.IsNullOrEmpty(keyVaultUri))
            {
                throw new InvalidConfigurationException(
                    "No key vault uri configured. Cannot connect.");
            }

            var builder = new ContainerBuilder();
            builder.AddDefaultAzureCredentials();
            builder.AddOptions();
            builder.Configure<CredentialOptions>(
                options => options.AllowInteractiveLogin = allowInteractiveLogon);
            builder.Configure<KeyVaultOptions>(
                options => options.KeyVaultBaseUrl = keyVaultUri);
            builder.RegisterType<KeyVaultConfig>()
                .AsImplementedInterfaces();
            builder.Register(c =>
            {
                var provider = c.Resolve<ICredentialProvider>();
                return new SecretClient(new Uri(keyVaultUri),
                    provider.Credential);
            }).AsSelf().AsImplementedInterfaces();
            _container = builder.Build();

            Client = _container.Resolve<SecretClient>();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _container.Dispose(); // Disposes keyvault client
        }

        private readonly IContainer _container;
    }
}
