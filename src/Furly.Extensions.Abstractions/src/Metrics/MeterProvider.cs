// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Metrics
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Default meter provider
    /// </summary>
    public sealed class MeterProvider : IMeterProvider
    {
        /// <summary>
        /// Default provider
        /// </summary>
        public static IMeterProvider Default { get; } = new MeterProvider();

        /// <inheritdoc/>
        public Meter Meter => new("Furly", "1.0");
    }
}
