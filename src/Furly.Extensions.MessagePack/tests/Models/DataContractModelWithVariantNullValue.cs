// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using Furly.Extensions.Serializers;
    using System.Runtime.Serialization;

    [DataContract]
    public class DataContractModelWithVariantNullValue
    {
        [DataMember(EmitDefaultValue = false, Order = 0)]
        public int Test1 { get; set; } = 4;

        [DataMember(EmitDefaultValue = false, Order = 1)]
        public VariantValue? Test2 { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        public string TestStr { get; set; } = "Test1";

        [DataMember(EmitDefaultValue = false, Order = 3)]
        public DataContractEnum? Test3 { get; set; } = DataContractEnum.Test1;

        public int Test4 { get; set; } = 4;
    }
}
