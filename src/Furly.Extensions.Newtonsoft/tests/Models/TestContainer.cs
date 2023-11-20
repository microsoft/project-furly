// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using Furly.Extensions.Serializers;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class TestContainer
    {
        [DataMember]
        public VariantValue? Value { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is TestContainer c && VariantValue.DeepEquals(c.Value, Value))
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return -1937169414 + EqualityComparer<VariantValue>.Default.GetHashCode(Value ?? VariantValue.Null);
        }
    }
}
