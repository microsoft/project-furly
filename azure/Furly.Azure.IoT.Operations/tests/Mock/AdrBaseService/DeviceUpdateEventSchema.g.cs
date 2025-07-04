/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.IoT.Operations.Mock.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.IoT.Operations.Mock;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class DeviceUpdateEventSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'device' Field.
        /// </summary>
        [JsonPropertyName("device")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Device Device { get; set; } = default!;

        /// <summary>
        /// The 'deviceName' Field.
        /// </summary>
        [JsonPropertyName("deviceName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DeviceName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
            if (DeviceName is null)
            {
                throw new ArgumentNullException("deviceName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
            if (DeviceName is null)
            {
                throw new ArgumentNullException("deviceName field cannot be null");
            }
        }
    }
}
