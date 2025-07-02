// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Runtime
{
    using System;

    /// <summary>
    /// Aio options
    /// </summary>
    public sealed record class AioOptions
    {
        /// <summary>
        /// Connector id
        /// </summary>
        public string? ConnectorId { get; set; }

        /// <summary>
        /// Identity used to compain over
        /// </summary>
        public string? Identity { get; internal set; }

        /// <summary>
        /// Name of the connector or workload
        /// </summary>
        public string? Name { get; internal set; }

        /// <summary>
        /// Length to stay leader
        /// </summary>
        public TimeSpan LeadershipTermLength { get; set; }
            = TimeSpan.FromHours(24);

        /// <summary>
        /// How often to check for leadership
        /// </summary>
        public TimeSpan LeadershipRenewalPeriod { get; set; }
            = TimeSpan.FromSeconds(30);
    }
}
