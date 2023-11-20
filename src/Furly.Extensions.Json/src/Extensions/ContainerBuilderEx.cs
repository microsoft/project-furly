// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Serializers.Json;

    /// <summary>
    /// All pluggable serializers
    /// </summary>
    public static class ContainerBuilderEx
    {
        /// <inheritdoc/>
        public static ContainerBuilder AddDefaultJsonSerializer(this ContainerBuilder builder)
        {
            builder.RegisterType<DefaultJsonSerializer>()
                .AsImplementedInterfaces();
            return builder;
        }
    }
}
