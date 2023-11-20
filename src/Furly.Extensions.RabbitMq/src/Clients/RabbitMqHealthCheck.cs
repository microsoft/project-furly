// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq.Clients
{
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;
    using RabbitMQ.Client;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Rabbitmq health checks
    /// </summary>
    public sealed class RabbitMqHealthCheck : IHealthCheck
    {
        /// <summary>
        /// Create health check
        /// </summary>
        /// <param name="config"></param>
        public RabbitMqHealthCheck(IOptionsSnapshot<RabbitMqOptions> config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config.Value.HostName,
                    Password = _config.Value.Key,
                    UserName = _config.Value.UserName
                };
                using var connection = factory.CreateConnection();
                if (connection == null)
                {
                    throw new IOException("Couldnt get connection");
                }
                using var model = connection.CreateModel();
                if (model?.IsClosed != false)
                {
                    throw new IOException("Couldnt get channel");
                }
                model.ExchangeDelete("test", true);
                return Task.FromResult(HealthCheckResult.Healthy());
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Cannot connect", ex));
            }
        }

        private readonly IOptionsSnapshot<RabbitMqOptions> _config;
    }
}
