// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Hosting
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Hosting extensions for Azure IoT Operations
    /// </summary>
    public static class HostingExtensions
    {
        /// <summary>
        /// Run the host as a leader in a distributed system.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task RunWithLeaderElectionAsync(this IHost host,
            CancellationToken ct = default)
        {
            var leaderElection = host.Services.GetService<ILeaderElection>();
            if (leaderElection != null)
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    // Wait until we are leader
                    await leaderElection.WaitAsync(ct).ConfigureAwait(false);
                    while (leaderElection.IsLeader)
                    {
                        try
                        {
                            await host.RunAsync(leaderElection.CancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                        // Lost the leadership, wait for the next election
                    }
                }
            }
            else
            {
                await host.RunAsync(ct).ConfigureAwait(false);
            }
        }
    }
}
