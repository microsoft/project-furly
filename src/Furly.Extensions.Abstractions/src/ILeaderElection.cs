// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Hosting
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Enables leader election for distributed systems.
    /// </summary>
    public interface ILeaderElection
    {
        /// <summary>
        /// Cancelled when not leader anymore
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Returns whether we are leader or not
        /// </summary>
        bool IsLeader { get; }

        /// <summary>
        /// Wait to become leader.
        /// </summary>
        /// <returns></returns>
        ValueTask WaitAsync(CancellationToken ct = default);
    }
}
