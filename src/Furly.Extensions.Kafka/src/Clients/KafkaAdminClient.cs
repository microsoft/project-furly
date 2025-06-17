// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Kafka;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Utils;
    using Confluent.Kafka;
    using Confluent.Kafka.Admin;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Kafka admin
    /// </summary>
    public sealed class KafkaAdminClient : IHealthCheck, IKafkaAdminClient, IDisposable
    {
        /// <summary>
        /// Create admin client
        /// </summary>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        /// <param name="identity"></param>
        public KafkaAdminClient(IOptionsSnapshot<KafkaServerOptions> config,
            ILogger<KafkaAdminClient> logger, IProcessIdentity? identity = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _admin = new AdminClientBuilder(config.Value.ToClientConfig<AdminClientConfig>(
                    identity?.Identity ?? Dns.GetHostName()))
                .SetErrorHandler(OnError)
                .SetLogHandler((_, m) => _logger.HandleKafkaMessage(m))
                .Build();
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            var status = _status ?? HealthCheckResult.Unhealthy();
            if (status.Status != HealthStatus.Healthy)
            {
                var metaData = await Try.Async(() => Task.Run(
                    () => _admin.GetMetadata(TimeSpan.FromSeconds(3)))).ConfigureAwait(false);
                if (metaData != null)
                {
                    // Reset to health
                    _status = status = HealthCheckResult.Healthy();
                }
            }
            return status;
        }

        /// <inheritdoc/>
        public async Task EnsureTopicExistsAsync(string topic)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _admin.CreateTopicsAsync(
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = _config.Value.Partitions,
                        ReplicationFactor = (short)_config.Value.ReplicaFactor,
                    }.YieldReturn(),
                    new CreateTopicsOptions
                    {
                        OperationTimeout = TimeSpan.FromSeconds(30),
                        RequestTimeout = TimeSpan.FromSeconds(30)
                    }).ConfigureAwait(false);
                _logger.LogInformation("Creating topic {Topic} took {Elapsed}.",
                    topic, sw.Elapsed);
            }
            catch (CreateTopicsException e)
            {
                if (e.Results.Count > 0 &&
                    e.Results[0].Error?.Code == ErrorCode.TopicAlreadyExists)
                {
                    _logger.LogInformation(
                        "Topic {Topic} already exists (check took {Elapsed}).",
                        topic, sw.Elapsed);
                    return;
                }
                _logger.LogError(e,
                    "Failed to create topic {Topic} (check took {Elapsed}).",
                        topic, sw.Elapsed);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _admin?.Dispose();
        }

        /// <summary>
        /// Handle error
        /// </summary>
        /// <param name="client"></param>
        /// <param name="error"></param>
        private void OnError(IClient client, Error error)
        {
            if (error.IsFatal)
            {
                _status = HealthCheckResult.Unhealthy(error.ToString());
            }
            else if (error.IsError)
            {
                _status = HealthCheckResult.Degraded(error.ToString());
            }
            else
            {
                _status = HealthCheckResult.Healthy();
            }
        }

        private HealthCheckResult? _status;
        private readonly ILogger _logger;
        private readonly IAdminClient _admin;
        private readonly IOptionsSnapshot<KafkaServerOptions> _config;
    }
}
