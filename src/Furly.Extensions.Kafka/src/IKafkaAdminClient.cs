// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using System.Threading.Tasks;

    /// <summary>
    /// Kafka adminstration interface
    /// </summary>
    public interface IKafkaAdminClient
    {
        /// <summary>
        /// Create topic
        /// </summary>
        /// <param name="topic"></param>
        Task EnsureTopicExistsAsync(string topic);
    }
}
