/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.IoT.Operations.Mock.AdrBaseService
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public enum MethodSchema
    {
        [EnumMember(Value = @"Anonymous")]
        Anonymous = 0,
        [EnumMember(Value = @"Certificate")]
        Certificate = 1,
        [EnumMember(Value = @"UsernamePassword")]
        UsernamePassword = 2,
    }
}
