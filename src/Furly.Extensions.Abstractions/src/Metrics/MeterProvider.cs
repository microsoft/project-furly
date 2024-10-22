// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Text;

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
        public Meter Meter => new Meter("Furly", "1.0");
    }
}
