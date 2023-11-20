// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage.Runtime
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Configure a specific container to open
    /// </summary>
    internal sealed class CollectionFactoryConfig : PostConfigureOptionBase<CollectionFactoryOptions>
    {
        /// <inheritdoc/>
        public CollectionFactoryConfig(IConfiguration configuration)
            : base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, CollectionFactoryOptions options)
        {
            if (string.IsNullOrEmpty(options.DatabaseName))
            {
                options.DatabaseName = "furly";
            }
            if (string.IsNullOrEmpty(options.ContainerName))
            {
                options.ContainerName = name;
            }
            if (string.IsNullOrEmpty(options.ContainerName))
            {
                options.ContainerName = "furly";
            }
        }
    }
}
