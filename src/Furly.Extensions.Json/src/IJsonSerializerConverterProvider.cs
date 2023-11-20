// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Converter provider
    /// </summary>
    public interface IJsonSerializerConverterProvider
    {
        /// <summary>
        /// Get converters
        /// </summary>
        IEnumerable<JsonConverter> GetConverters();
    }
}
