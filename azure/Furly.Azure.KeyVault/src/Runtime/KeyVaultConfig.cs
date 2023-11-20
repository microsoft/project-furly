// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.KeyVault.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <inheritdoc/>
    public sealed class KeyVaultConfig : PostConfigureOptionBase<KeyVaultOptions>
    {
        /// <inheritdoc/>
        public KeyVaultConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, KeyVaultOptions options)
        {
            if (string.IsNullOrEmpty(options.KeyVaultBaseUrl))
            {
                options.KeyVaultBaseUrl = GetStringOrDefault("KEYVAULT__BASEURL",
                    GetStringOrDefault(EnvironmentVariables.PCS_KEYVAULT_URL,
                    string.Empty)).Trim();
            }
        }
    }
}
