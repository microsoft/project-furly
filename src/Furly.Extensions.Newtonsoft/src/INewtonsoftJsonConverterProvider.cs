// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using global::Newtonsoft.Json;
    using System.Collections.Generic;

    /// <summary>
    /// Converter provider
    /// </summary>
    public interface INewtonsoftJsonConverterProvider
    {
        /// <summary>
        /// Get converters
        /// </summary>
        IEnumerable<JsonConverter> GetConverters();
    }
}
