// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Extensions.Serializers;
    using System.Collections.Generic;

    /// <summary>
    /// Edge twin client
    /// </summary>
    public interface IIoTEdgeTwinClient
    {
        /// <summary>
        /// Access to the twin state. You can add or remove your
        /// state and it will be reported eventually to server
        /// side.
        /// </summary>
        IDictionary<string, VariantValue> Twin { get; }
    }
}
