// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Logging
{
    using Autofac.Features.Metadata;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health checks
    /// </summary>
    public sealed class HealthCheckRegistrar : IOptions<HealthCheckServiceOptions>
    {
        /// <inheritdoc/>
        public HealthCheckServiceOptions Value { get; }

        /// <summary>
        /// Register checks
        /// </summary>
        /// <param name="checks"></param>
        public HealthCheckRegistrar(IEnumerable<Meta<IHealthCheck>> checks)
        {
            Value = new HealthCheckServiceOptions();
            foreach (var check in checks)
            {
                var name = check.Value.GetType().FullName;
                if (name is null)
                {
                    throw new InvalidOperationException("Type name is null");
                }
                Value.Registrations.Add(new HealthCheckRegistration(
                    name, check.Value, null, check.Metadata.Keys));
            }
        }
    }
}
