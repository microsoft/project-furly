// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class DataContractModelWithDefaultValues
    {
        [DataMember(EmitDefaultValue = false)]
        public int Test1 { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string? Test2 { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public DataContractEnum? Test3 { get; set; }

        public int Test4 { get; set; } = 4;
    }
}
