// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr
{
    using Grpc.Net.Client;

    /// <summary>
    /// Dapr configuration
    /// </summary>
    public class DaprOptions
    {
        /// <summary>
        /// The pub sub component to use. If not specified
        /// the first part of the topic path will be used.
        /// </summary>
        public string? PubSubComponent { get; set; }

        /// <summary>
        /// The name of the state store to use. If not
        /// specified the name used is "default".
        /// </summary>
        public string? StateStoreName { get; set; }

        /// <summary>
        /// Api token secret
        /// </summary>
        public string? ApiToken { get; set; }

        /// <summary>
        /// Http endpoint to use (optional)
        /// </summary>
        public string? HttpEndpoint { get; set; }

        /// <summary>
        /// Grpc endpoint to use (optional)
        /// </summary>
        public string? GrpcEndpoint { get; set; }

        /// <summary>
        /// The value configured as max message size
        /// (Default: 512 MB)
        /// </summary>
        public int? MessageMaxBytes { get; set; }

        /// <summary>
        /// Grpc channel options
        /// </summary>
        public GrpcChannelOptions GrpcChannelOptions { get; } = new();
    }
}
