// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Server.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class TestResponseModel
    {
        [DataMember]
        public string? Id { get; set; }
        [DataMember]
        public string? Input { get; set; }
        [DataMember]
        public string? Method { get; set; }
    }
}
