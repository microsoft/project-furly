// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Serializers;
    using global::Dapr.Client;

    internal static class Extensions
    {
        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static DaprClient CreateClient(this DaprOptions options,
            IJsonSerializerSettingsProvider? provider = null)
        {
            var builder = new DaprClientBuilder()
                .UseGrpcChannelOptions(options.GrpcChannelOptions)
                ;
            if (options.ApiToken != null)
            {
                builder.UseDaprApiToken(options.ApiToken);
            }
            if (options.HttpEndpoint != null)
            {
                builder.UseHttpEndpoint(options.HttpEndpoint);
            }
            if (options.GrpcEndpoint != null)
            {
                builder.UseGrpcEndpoint(options.GrpcEndpoint);
            }
            if (provider != null)
            {
                builder.UseJsonSerializationOptions(provider.Settings);
            }
            return builder.Build();
        }
    }
}
