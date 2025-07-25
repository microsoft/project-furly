/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.IoT.Operations.Mock.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.IoT.Operations.Mock;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class DeviceUpdateEventTelemetry : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'deviceUpdateEvent' Telemetry.
        /// </summary>
        [JsonPropertyName("deviceUpdateEvent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DeviceUpdateEventSchema DeviceUpdateEvent { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DeviceUpdateEvent is null)
            {
                throw new ArgumentNullException("deviceUpdateEvent field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DeviceUpdateEvent is null)
            {
                throw new ArgumentNullException("deviceUpdateEvent field cannot be null");
            }
        }
    }
}
