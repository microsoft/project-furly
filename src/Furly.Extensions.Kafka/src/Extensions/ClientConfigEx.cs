// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Confluent.Kafka
{
    using Furly.Extensions.Kafka;
    using System;

    /// <summary>
    /// Client configuration extensions
    /// </summary>
    internal static class ClientConfigEx
    {
        /// <summary>
        /// Create configuration
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T ToClientConfig<T>(this KafkaServerOptions options,
            string? clientId = null) where T : ClientConfig, new()
        {
            if (string.IsNullOrEmpty(options?.BootstrapServers))
            {
                throw new ArgumentException("Missing bootstrap server", nameof(options));
            }
            return new T
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = clientId,
                // ...
            };
        }
    }
}
