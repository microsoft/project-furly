// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Furly.Extensions.Messaging;
    using Furly.Azure;

    /// <summary>
    /// DI extension
    /// </summary>
    public static class ServiceCollectionEx
    {
        /// <summary>
        /// Add event client
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddDefaultAzureCredentials(this IServiceCollection services)
        {
            return services
                .AddScoped<ICredentialProvider, DefaultAzureCredentials>()
                .AddOptions()
                ;
        }
    }
}
