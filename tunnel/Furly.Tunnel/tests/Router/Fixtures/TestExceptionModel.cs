// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using Furly.Extensions.Serializers;
    using System.Runtime.Serialization;

    /// <summary>
    /// Method call exception model.
    /// </summary>
    [DataContract]
    public class TestExceptionModel
    {
        /// <summary>
        /// Exception message.
        /// </summary>
        [DataMember(Name = "message", Order = 0,
            EmitDefaultValue = true)]
        public string? Message { get; set; }

        /// <summary>
        /// Details of the exception.
        /// </summary>
        [DataMember(Name = "details", Order = 1,
            EmitDefaultValue = true)]
        public VariantValue? Details { get; set; }
    }
}
