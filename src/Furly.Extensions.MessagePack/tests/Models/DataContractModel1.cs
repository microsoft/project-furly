// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class DataContractModel1
    {
        [DataMember(Name = "a", EmitDefaultValue = false)]
        public int Test1 { get; set; } = 8;

        [DataMember(Name = "b", EmitDefaultValue = false)]
        public string? Test2 { get; set; }
    }
}
