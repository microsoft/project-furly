// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Tunneled message
    /// </summary>
    [DataContract]
    public class HttpTunnelRequestModel
    {
        /// <summary>
        /// Message contains request
        /// </summary>
        public const string SchemaName =
            "application/x-http-tunnel-request-v1";

        /// <summary>
        /// Method
        /// </summary>
        [DataMember(Name = "method", Order = 0)]
        public string Method { get; set; } = null!;

        /// <summary>
        /// Request identifier
        /// </summary>
        [DataMember(Name = "requestId", Order = 1)]
        public string RequestId { get; set; } = null!;

        /// <summary>
        /// Uri to call
        /// </summary>
        [DataMember(Name = "uri", Order = 2)]
        public string Uri { get; set; } = null!;

        /// <summary>
        /// Headers
        /// </summary>
        [DataMember(Name = "requestHeaders", Order = 3,
            EmitDefaultValue = false, IsRequired = false)]
        public Dictionary<string, List<string>>? RequestHeaders { get; set; }

        /// <summary>
        /// Headers
        /// </summary>
        [DataMember(Name = "contentHeaders", Order = 4,
            EmitDefaultValue = false, IsRequired = false)]
        public Dictionary<string, List<string>>? ContentHeaders { get; set; }

        /// <summary>
        /// Body (optional - can follow this info)
        /// </summary>
        [DataMember(Name = "body", Order = 5,
            EmitDefaultValue = false, IsRequired = false)]
        public byte[]? Body { get; set; }
    }
}
