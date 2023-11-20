// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using global::Newtonsoft.Json;

    /// <summary>
    /// Json.net serializer settings provider
    /// </summary>
    public interface INewtonsoftSerializerSettingsProvider
    {
        /// <summary>
        /// Serializer settings
        /// </summary>
        JsonSerializerSettings Settings { get; }
    }
}
