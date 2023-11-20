// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using global::MessagePack;
    using System.Collections.Generic;

    /// <summary>
    /// Message pack serializer options provider
    /// </summary>
    public interface IMessagePackSerializerOptionsProvider
    {
        /// <summary>
        /// Serializer options
        /// </summary>
        MessagePackSerializerOptions Options { get; }

        /// <summary>
        /// Resolvers
        /// </summary>
        IEnumerable<IFormatterResolver> Resolvers { get; }
    }
}
