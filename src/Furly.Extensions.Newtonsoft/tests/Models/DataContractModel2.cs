// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class DataContractModel2
    {
        [DataMember(EmitDefaultValue = false)]
        public int Test1 { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string? Test2 { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IReadOnlyCollection<byte>? Bytes { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public ISet<string>? Set { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IReadOnlySet<string>? RoSet { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IList<string>? Strings { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IReadOnlyList<string>? RoStrings { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IList<IList<string>>? StringsOfStrings { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IReadOnlyList<IReadOnlyList<string>>? RoStringsOfStrings { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IDictionary<string, string>? Dictionary { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IReadOnlyDictionary<string, string>? RoDictionary { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is DataContractModel2 model)
            {
                if (Test1 != model.Test1)
                {
                    return false;
                }
                if (Test2 != model.Test2)
                {
                    return false;
                }
                if (!Bytes.SequenceEqualsSafe(model.Bytes))
                {
                    return false;
                }
                if (!Set.SetEqualsSafe(model.Set))
                {
                    return false;
                }
                if (!RoSet.SetEqualsSafe(model.RoSet))
                {
                    return false;
                }
                if (!Strings.SequenceEqualsSafe(model.Strings))
                {
                    return false;
                }
                if (!RoStrings.SequenceEqualsSafe(model.RoStrings))
                {
                    return false;
                }
                if (!StringsOfStrings.SequenceEqualsSafe(
                    model.StringsOfStrings, (x, y) => x.SequenceEqualsSafe(y)))
                {
                    return false;
                }
                if (!RoStringsOfStrings.SequenceEqualsSafe(
                    model.RoStringsOfStrings, (x, y) => x.SequenceEqualsSafe(y)))
                {
                    return false;
                }
                if (!Dictionary.DictionaryEqualsSafe(model.Dictionary))
                {
                    return false;
                }
                if (!RoDictionary.DictionaryEqualsSafe(model.RoDictionary))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}
