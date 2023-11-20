// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System.Text.Json;

    /// <summary>
    /// Json serializer settings provider
    /// </summary>
    public interface IJsonSerializerSettingsProvider
    {
        /// <summary>
        /// Serializer settings
        /// </summary>
        JsonSerializerOptions Settings { get; }
    }
}
