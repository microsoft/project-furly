// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using global::MessagePack;
    using System.Collections.Generic;

    /// <summary>
    /// Formtter provider
    /// </summary>
    public interface IMessagePackFormatterResolverProvider
    {
        /// <summary>
        /// Get Resolvers
        /// </summary>
        IEnumerable<IFormatterResolver> GetResolvers();
    }
}
