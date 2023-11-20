// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    /// <summary>
    /// IoT Edge client configuration
    /// </summary>
    public class IoTEdgeClientOptions
    {
        /// <summary>
        /// EdgeHub connection string
        /// </summary>
        public string? EdgeHubConnectionString { get; set; }

        /// <summary>
        /// Transports to use
        /// </summary>
        public TransportOption Transport { get; set; }

        /// <summary>
        /// Product name to use
        /// </summary>
        public string? Product { get; set; }
    }
}
