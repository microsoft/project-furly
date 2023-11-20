// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Determines where in the configuration providers chain current provider should
    /// be added.
    /// </summary>
    public enum ConfigSourcePriority
    {
        /// <summary>
        /// Normal insertion - last provider added that has the value wins
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Will be inserted at start of chain - every other provider with
        /// value will win.   This can be used to set base line values.
        /// </summary>
        Low = 1
    }
}
