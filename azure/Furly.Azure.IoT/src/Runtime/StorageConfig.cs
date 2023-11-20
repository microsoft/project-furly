// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using System;

    /// <summary>
    /// Storage configuration
    /// </summary>
    internal sealed class StorageConfig : PostConfigureOptionBase<StorageOptions>
    {
        /// <inheritdoc/>
        public StorageConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, StorageOptions options)
        {
            if (string.IsNullOrEmpty(options.AccountName))
            {
                options.AccountName = GetConnectonStringTokenOrDefault(
                    EnvironmentVariables.PCS_STORAGE_CONNSTRING, cs => cs.Endpoint,
                    GetStringOrDefault("PCS_ASA_DATA_AZUREBLOB_ACCOUNT",
                    GetStringOrDefault("PCS_IOTHUBREACT_AZUREBLOB_ACCOUNT", string.Empty)));
            }
            if (string.IsNullOrEmpty(options.EndpointSuffix))
            {
                options.EndpointSuffix = GetConnectonStringTokenOrDefault(
                    EnvironmentVariables.PCS_STORAGE_CONNSTRING, cs => cs.EndpointSuffix,
                    GetStringOrDefault("PCS_ASA_DATA_AZUREBLOB_ENDPOINT_SUFFIX",
                    GetStringOrDefault("PCS_IOTHUBREACT_AZUREBLOB_ENDPOINT_SUFFIX",
                    "core.windows.net")));
            }
            if (string.IsNullOrEmpty(options.AccountKey))
            {
                options.AccountKey = GetConnectonStringTokenOrDefault(
                    EnvironmentVariables.PCS_STORAGE_CONNSTRING, cs => cs.SharedAccessKey,
                    GetStringOrDefault("PCS_ASA_DATA_AZUREBLOB_KEY",
                    GetStringOrDefault("PCS_IOTHUBREACT_AZUREBLOB_KEY", string.Empty)));
            }
        }

        /// <summary>
        /// Read variable and get connection string token from it
        /// </summary>
        /// <param name="key"></param>
        /// <param name="getter"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private string GetConnectonStringTokenOrDefault(string key,
            Func<ConnectionString, string?> getter, string? defaultValue = null)
        {
            var value = Configuration.GetValue<string>(key);
            if (string.IsNullOrEmpty(value)
                || !ConnectionString.TryParse(value.Trim(), out var cs)
                || string.IsNullOrEmpty(value = getter(cs)))
            {
                return defaultValue ?? string.Empty;
            }
            return value;
        }
    }
}
