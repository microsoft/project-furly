// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Runtime
{
    /// <summary>
    /// Overflow strategy
    /// </summary>
    public enum OverflowStrategy
    {
        /// <summary>
        /// Drop oldest
        /// </summary>
        DropOldestQueuedMessage,

        /// <summary>
        /// Drop newest
        /// </summary>
        DropNewMessage
    }
}
