// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Dapr configuration
    /// </summary>
    internal sealed class DaprConfig : PostConfigureOptionBase<DaprOptions>
    {
        /// <inheritdoc/>
        public DaprConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, DaprOptions options)
        {
            if (string.IsNullOrEmpty(options.ApiToken))
            {
                options.ApiToken =
                    GetStringOrDefault(EnvironmentVariable.DAPRAPITOKEN);
            }
            if (string.IsNullOrEmpty(options.GrpcEndpoint))
            {
                options.GrpcEndpoint =
                    GetStringOrDefault(EnvironmentVariable.DAPRGRPCENDPOINT);
            }
            if (string.IsNullOrEmpty(options.HttpEndpoint))
            {
                options.HttpEndpoint =
                    GetStringOrDefault(EnvironmentVariable.DAPRHTTPENDPOINT);
            }

            options.GrpcChannelOptions.ThrowOperationCanceledOnCancellation = true;
        }
    }
}
