// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Hosting;
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.LeaderElection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Leader election client
    /// </summary>
    public sealed class AioLeClient : ILeaderElection, IDisposable
    {
        /// <inheritdoc/>
        public CancellationToken CancellationToken => _cts.Token;

        /// <inheritdoc/>
        public bool IsLeader { get; private set; }

        /// <summary>
        /// Create aio leader election client
        /// </summary>
        /// <param name="sdk"></param>
        /// <param name="client"></param>
        /// <param name="logger"></param>
        public AioLeClient(IAioSdk sdk, IMqttPubSubClient client, ILogger<AioLeClient> logger)
        {
            _logger = logger;
            _cts = new CancellationTokenSource();
            _client = sdk.CreateLeaderElectionClient(client);
            _client.LeadershipChangeEventReceivedAsync += async (_, args) =>
            {
                IsLeader = args.NewLeader != null &&
                    args.NewLeader.GetString().Equals(client.ClientId);
                if (IsLeader)
                {
                    _logger.PodGainedLeadership();
                }
                else
                {
                    _logger.PodLostLeadership(args.NewLeader?.GetString());
                    var cts = _cts;
                    _cts = new CancellationTokenSource();
                    await cts.CancelAsync().ConfigureAwait(false);
                    cts.Dispose();
                }
            };
            _logger.StartLeadershipElection(client.ClientId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                _logger.StopLeadershipElection();
                IsLeader = false;
                await _cts.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                _cts.Dispose();
                await _client.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask WaitAsync(CancellationToken ct)
        {
            while (!IsLeader)
            {
                ct.ThrowIfCancellationRequested();
                _logger.PodWaitingForLeadership();
                // Waits until elected leader
                await _client.CampaignAsync(_client.AutomaticRenewalOptions.ElectionTerm,
                    cancellationToken: ct).ConfigureAwait(false);
                IsLeader = true;
                _logger.PodGainedLeadership();
            }
        }

        private readonly ILeaderElectionClient _client;
        private CancellationTokenSource _cts;
        private readonly ILogger<AioLeClient> _logger;
    }

    /// <summary>
    /// Source-generated logging for AioAdrClient
    /// </summary>
    internal static partial class AioLeClientLogging
    {
        private const int EventClass = 40;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Information,
            Message = "Start leadership election using client {ClientId}")]
        public static partial void StartLeadershipElection(this ILogger logger, string? clientId);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Information,
            Message = "Stop leadership election.")]
        public static partial void StopLeadershipElection(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Information,
            Message = "This pod was elected leader")]
        public static partial void PodGainedLeadership(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 3, Level = LogLevel.Information,
            Message = "This pod is waiting to be elected leader.")]
        public static partial void PodWaitingForLeadership(this ILogger logger);

        [LoggerMessage(EventId = EventClass + 4, Level = LogLevel.Information,
            Message = "This pod lost its leadership to {ClientId}")]
        public static partial void PodLostLeadership(this ILogger logger, string? clientId);
    }
}
