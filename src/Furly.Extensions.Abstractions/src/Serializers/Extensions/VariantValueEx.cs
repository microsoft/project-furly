// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    /// <summary>
    /// Variant extensions
    /// </summary>
    public static class VariantValueEx
    {
        /// <summary>
        /// Test for null
        /// </summary>
        /// <param name="value"></param>
        public static bool IsNull(this VariantValue? value)
        {
            return VariantValue.IsNullOrNullValue(value);
        }
    }
}
