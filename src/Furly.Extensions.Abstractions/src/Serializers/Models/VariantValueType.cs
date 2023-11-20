// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    /// <summary>
    /// Variant discriminator
    /// </summary>
    public enum VariantValueType
    {
        /// <summary>
        /// Null
        /// </summary>
        Null = 0,

        /// <summary>
        /// Array
        /// </summary>
        Values = 1,

        /// <summary>
        /// Object
        /// </summary>
        Complex = 2,

        /// <summary>
        /// String
        /// </summary>
        Primitive = 3
    }
}
