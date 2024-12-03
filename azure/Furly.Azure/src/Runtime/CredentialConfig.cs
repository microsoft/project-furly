// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Credential configuration
    /// </summary>
    public sealed class CredentialConfig : PostConfigureOptionBase<CredentialOptions>
    {
        /// <inheritdoc/>
        public CredentialConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, CredentialOptions options)
        {
            options.AllowInteractiveLogin ??= GetBoolOrNull(EnvironmentVariables.PCS_ALLOW_INTERACTIVE_LOGIN);
        }
    }
}
