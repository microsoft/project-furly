/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.IoT.Operations.Mock.DeviceDiscoveryService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.IoT.Operations.Mock;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class CreateOrUpdateDiscoveredDeviceResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("discoveredDeviceResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DiscoveredDeviceResponseSchema DiscoveredDeviceResponse { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredDeviceResponse is null)
            {
                throw new ArgumentNullException("discoveredDeviceResponse field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredDeviceResponse is null)
            {
                throw new ArgumentNullException("discoveredDeviceResponse field cannot be null");
            }
        }
    }
}
