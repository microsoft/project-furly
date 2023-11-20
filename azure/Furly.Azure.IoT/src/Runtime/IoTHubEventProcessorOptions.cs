// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    using System;

    /// <summary>
    /// Event hub configuration
    /// </summary>
    public class IoTHubEventProcessorOptions
    {
        /// <summary>
        /// Event hub endpoint
        /// </summary>
        public string? EventHubEndpoint { get; set; }

        /// <summary>
        /// Whether to use websockets
        /// </summary>
        public bool UseWebsockets { get; set; }

        /// <summary>
        /// Consumer group
        /// (optional, default to $default)
        /// </summary>
        public string? ConsumerGroup { get; set; }

        /// <summary>
        /// Receive timeout
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; }

        /// <summary>
        /// Whether to read from end or start.
        /// </summary>
        public bool InitialReadFromStart { get; set; }

        /// <summary>
        /// Set checkpoint interval. null = never.
        /// </summary>
        public TimeSpan? CheckpointInterval { get; set; }

        /// <summary>
        /// Skip all events older than. null = never.
        /// </summary>
        public TimeSpan? SkipEventsOlderThan { get; set; }
    }
}
