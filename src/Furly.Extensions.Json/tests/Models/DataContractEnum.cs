// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using System;
    using System.Runtime.Serialization;

    [Flags]
    [DataContract]
    public enum DataContractEnum
    {
        [EnumMember(Value = "tst1")]
        Test1 = 1,
        [EnumMember]
        Test2 = 2,
        [EnumMember]
        Test3 = 4,
        [EnumMember]
        All = Test1 | Test2 | Test3
    }
}
