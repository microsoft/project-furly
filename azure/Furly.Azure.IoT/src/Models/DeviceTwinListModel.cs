// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// List of device twins with continuation token
    /// </summary>
    [DataContract]
    public sealed record class DeviceTwinListModel
    {
        /// <summary>
        /// Continuation token to use for next call or null
        /// </summary>
        [DataMember(Name = "continuationToken",
            EmitDefaultValue = false)]
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Items returned
        /// </summary>
        [DataMember(Name = "items")]
        public IReadOnlyList<DeviceTwinModel> Items { get; set; } = null!;
    }
}
