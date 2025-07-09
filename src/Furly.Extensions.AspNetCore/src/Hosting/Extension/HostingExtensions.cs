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
        /// Build the host and run it while this process is the leader
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task RunAsync(this IHostBuilder builder, CancellationToken ct = default)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                using var host = builder.Build();
                var leaderElection = host.Services.GetService<ILeaderElection>();
                if (leaderElection == null)
                {
                    // No leader election service configured, run the host directly and when done exit
                    await host.RunAsync(ct).ConfigureAwait(false);
                    break;
                }

                // Wait until we are leader and then start running until we loose leadership
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
    }
}
