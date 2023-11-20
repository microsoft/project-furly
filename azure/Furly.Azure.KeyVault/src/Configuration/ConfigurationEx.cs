// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.Configuration
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Furly.Azure.KeyVault;
    using Furly.Exceptions;
    using Furly.Extensions.Logging;
    using global::Azure;
    using global::Azure.Security.KeyVault.Secrets;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods
    /// </summary>
    public static class ConfigurationEx
    {
        /// <summary>
        /// Add configuration from Azure KeyVault. Providers configured prior to
        /// this one will be used to get Azure KeyVault connection details.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="providerPriority"> Determines where in the configuration
        /// providers chain current provider should be added. Default to lowest
        /// </param>
        /// <param name="allowInteractiveLogon"></param>
        /// <param name="singleton"></param>
        /// <param name="keyVaultUrlVarName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static IConfigurationBuilder AddFromKeyVault(this IConfigurationBuilder builder,
            ConfigurationProviderPriority providerPriority = ConfigurationProviderPriority.Lowest,
            bool allowInteractiveLogon = false, bool singleton = true, string? keyVaultUrlVarName = null)
        {
            var configuration = builder.Build();

            // Check if configuration should be loaded from KeyVault, default to true.
            var keyVaultConfigEnabled = configuration.GetValue(
                Furly.Azure.KeyVault.EnvironmentVariables.PCS_KEYVAULT_CONFIG_ENABLED, true);
            if (!keyVaultConfigEnabled)
            {
                return builder;
            }

            var provider = KeyVaultConfigurationProvider.CreateInstanceAsync(
                allowInteractiveLogon, singleton, configuration, keyVaultUrlVarName).Result;
            if (provider != null)
            {
                switch (providerPriority)
                {
                    case ConfigurationProviderPriority.Highest:
                        builder.Add(provider);
                        break;
                    case ConfigurationProviderPriority.Lowest:
                        builder.Sources.Insert(0, provider);
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unknown ConfigurationProviderPriority value: {providerPriority}");
                }
            }
            return builder;
        }

        /// <summary>
        /// Keyvault configuration provider.
        /// </summary>
        internal sealed class KeyVaultConfigurationProvider : IConfigurationSource,
            IConfigurationProvider, IDisposable
        {
            /// <summary>
            /// Create keyvault provider
            /// </summary>
            /// <param name="keyVaultUri"></param>
            /// <param name="allowInteractiveLogon"></param>
            private KeyVaultConfigurationProvider(
                string keyVaultUri, bool allowInteractiveLogon)
            {
                _keyVault = new KeyVaultClientBootstrap(keyVaultUri, allowInteractiveLogon);
                _cache = new ConcurrentDictionary<string, Task<Response<KeyVaultSecret>>>();
                _reloadToken = new ConfigurationReloadToken();
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _keyVault.Dispose();
            }

            /// <inheritdoc/>
            public IConfigurationProvider Build(IConfigurationBuilder builder)
            {
                return this;
            }

            /// <inheritdoc/>
            public bool TryGet(string key, out string? value)
            {
                value = null;
                try
                {
                    if (_allSecretsLoaded && !_cache.ContainsKey(key))
                    {
                        // Prevents non existant keys to be looked up
                        return false;
                    }
                    var resultTask = _cache.GetOrAdd(key,
                        k => _keyVault.Client.GetSecretAsync(GetSecretNameForKey(k)));
                    if (resultTask.IsFaulted || resultTask.IsCanceled)
                    {
                        return false;
                    }
                    value = resultTask.Result.Value.Value;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <inheritdoc/>
            public void Set(string key, string? value)
            {
                // No op
            }

            /// <inheritdoc/>
            public void Load()
            {
                // No op
            }

            /// <inheritdoc/>
            public IChangeToken GetReloadToken()
            {
                return _reloadToken;
            }

            public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys,
                string? parentPath)
            {
                // Not supported
                return Enumerable.Empty<string>();
            }

            /// <summary>
            /// Create configuration provider
            /// </summary>
            /// <param name="allowInteractiveLogon"></param>
            /// <param name="singleton"></param>
            /// <param name="configuration"></param>
            /// <param name="keyVaultUrlVarName"></param>
            /// <returns></returns>
            public static async Task<KeyVaultConfigurationProvider?> CreateInstanceAsync(
                bool allowInteractiveLogon, bool singleton, IConfigurationRoot configuration,
                string? keyVaultUrlVarName)
            {
                if (string.IsNullOrEmpty(keyVaultUrlVarName))
                {
                    keyVaultUrlVarName = Furly.Azure.KeyVault.EnvironmentVariables.PCS_KEYVAULT_URL;
                }
                if (singleton && !allowInteractiveLogon)
                {
                    // Safe singleton creation
                    if (_singleton == null)
                    {
                        lock (kLock)
                        {
                            // Create instance
                            _singleton = CreateInstanceAsync(configuration, false,
                                keyVaultUrlVarName, false);
                        }
                    }
                    return await _singleton.ConfigureAwait(false);
                }
                // Create new instance
                return await CreateInstanceAsync(configuration, allowInteractiveLogon,
                    keyVaultUrlVarName, true).ConfigureAwait(false);
            }

            /// <summary>
            /// Create new instance
            /// </summary>
            /// <param name="configuration"></param>
            /// <param name="allowInteractiveLogon"></param>
            /// <param name="keyVaultUrlVarName"></param>
            /// <param name="lazyLoad"></param>
            /// <returns></returns>
            /// <exception cref="InvalidConfigurationException"></exception>
            private static async Task<KeyVaultConfigurationProvider?> CreateInstanceAsync(
                IConfigurationRoot configuration, bool allowInteractiveLogon, string keyVaultUrlVarName,
                bool lazyLoad)
            {
                var logger = Log.Console<KeyVaultConfigurationProvider>();
                var vaultUri = configuration.GetValue<string?>(keyVaultUrlVarName, null);
                if (string.IsNullOrEmpty(vaultUri))
                {
                    logger.LogDebug("No keyvault uri found in configuration under {Key}. ",
                        keyVaultUrlVarName);
                    vaultUri = Environment.GetEnvironmentVariable(keyVaultUrlVarName);
                    if (string.IsNullOrEmpty(vaultUri))
                    {
                        logger.LogDebug("No keyvault uri found in environment under {Key}. " +
                            "Not reading configuration from keyvault without keyvault uri.",
                            keyVaultUrlVarName);
                        return null;
                    }
                }
                var provider = new KeyVaultConfigurationProvider(vaultUri, allowInteractiveLogon);
                try
                {
                    await provider.ValidateReadSecretAsync(keyVaultUrlVarName).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidConfigurationException(
                        "A keyvault uri was provided could not access keyvault at the address. " +
                        "If you want to read configuration from keyvault, make sure " +
                        "the keyvault is reachable, the required permissions are configured " +
                        "on keyvault and authentication provider information is available. " +
                        "Sign into Visual Studio or Azure CLI on this machine and try again.", ex);
                }
                if (!lazyLoad)
                {
                    while (true)
                    {
                        try
                        {
                            await provider.LoadAllSecretsAsync().ConfigureAwait(false);
                            break;
                        }
                        // try again...
                        catch (TaskCanceledException) { }
                        catch (SocketException) { }
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        logger.LogInformation(
                            "Failed loading secrets due to timeout or network - try again ...");
                    }
                }
                return provider;
            }

            /// <summary>
            /// Read configuration secret
            /// </summary>
            /// <param name="secretName"></param>
            /// <returns></returns>
            /// <exception cref="TimeoutException"></exception>
            private async Task ValidateReadSecretAsync(string secretName)
            {
                for (var retries = 0; ; retries++)
                {
                    try
                    {
                        var secret = await _keyVault.Client.GetSecretAsync(
                            GetSecretNameForKey(secretName)).ConfigureAwait(false);
                        // Worked - we have a working keyvault client.
                        return;
                    }
                    catch (TaskCanceledException) { }
                    catch (SocketException) { }
                    if (retries > 3)
                    {
                        throw new TimeoutException(
                            "Failed to access keyvault due to timeout or network.");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Preload cache
            /// </summary>
            /// <returns></returns>
            private async Task LoadAllSecretsAsync(CancellationToken ct = default)
            {
                await foreach (var secretProperty in _keyVault.Client.GetPropertiesOfSecretsAsync(ct))
                {
                    if (secretProperty.Enabled != true)
                    {
                        continue;
                    }
                    var key = GetKeyForSecretName(secretProperty.Name);
                    if (key == null)
                    {
                        continue;
                    }
                    _cache.TryAdd(key, _keyVault.Client.GetSecretAsync(secretProperty.Name,
                        cancellationToken: ct));
                }
                _allSecretsLoaded = true;
                await Task.WhenAll(_cache.Values).ConfigureAwait(false);
            }

            /// <summary>
            /// Get secret key for key value. Replace any upper case
            /// letters with lower case and _ with -.
            /// </summary>
            /// <param name="key"></param>
            /// <returns></returns>
            private static string GetSecretNameForKey(string key)
            {
#pragma warning disable CA1308 // Normalize strings to uppercase
                return key.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
            }

            /// <summary>
            /// Get secret key for key value. Replace any upper case
            /// letters with lower case and _ with -.
            /// </summary>
            /// <param name="secretId"></param>
            /// <returns></returns>
            private static string? GetKeyForSecretName(string secretId)
            {
                return secretId.Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant();
            }

            private static readonly object kLock = new();
            private static Task<KeyVaultConfigurationProvider?>? _singleton;
            private readonly KeyVaultClientBootstrap _keyVault;
            private readonly ConcurrentDictionary<string, Task<Response<KeyVaultSecret>>> _cache;
            private readonly ConfigurationReloadToken _reloadToken;
            private bool _allSecretsLoaded;
        }
    }
}
