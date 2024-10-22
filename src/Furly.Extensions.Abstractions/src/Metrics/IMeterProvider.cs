// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Metrics
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Provides a meter
    /// </summary>
    public interface IMeterProvider
    {
        /// <summary>
        /// Meter to use
        /// </summary>
        Meter Meter { get; }
    }
}
