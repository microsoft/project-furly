/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.IoT.Operations.Mock.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.IoT.Operations.Mock;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class GetAssetResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("asset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Asset Asset { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
        }
    }
}
