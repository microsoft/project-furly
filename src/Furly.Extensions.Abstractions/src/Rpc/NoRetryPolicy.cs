// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc
{
    using System;

    /// <summary>
    /// A retry policy that will never retry.
    /// </summary>
    public class NoRetryPolicy : IRetryPolicy
    {
        /// <inheritdoc/>
        public bool ShouldRetry(uint currentRetryCount,
            Exception lastException, out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.Zero;
            return false;
        }
    }
}
