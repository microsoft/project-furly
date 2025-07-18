// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

namespace Azure.IoT.Operations.Mock
{
    using System;
    using System.Buffers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1515 // Consider making public types internal
    public class Utf8JsonSerializer : IPayloadSerializer
#pragma warning restore CA1515 // Consider making public types internal
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string ContentType = "application/json";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected static readonly JsonSerializerOptions JsonSerializerOptions = new ()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new DurationJsonConverter(),
                new DateJsonConverter(),
                new TimeJsonConverter(),
                new UuidJsonConverter(),
                new BytesJsonConverter(),
                new DecimalJsonConverter(),
            },
        };

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
            where T : class
        {
            if (contentType != null && contentType != ContentType)
            {
                throw new AkriMqttException($"Content type {contentType} is not supported by this implementation; only {ContentType} is accepted.")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    HeaderName = "Content Type",
                    HeaderValue = contentType,
                    IsShallow = false,
                    IsRemote = false,
                };
            }

            try
            {
                if (payload.IsEmpty)
                {
                    if (typeof(T) != typeof(EmptyJson))
                    {
                        throw AkriMqttException.GetPayloadInvalidException();
                    }

                    return (new EmptyJson() as T) !;
                }

                Utf8JsonReader reader = new (payload);
                return JsonSerializer.Deserialize<T>(ref reader, JsonSerializerOptions) !;
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public SerializedPayloadContext ToBytes<T>(T? payload)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(EmptyJson))
                {
                    return new (ReadOnlySequence<byte>.Empty, null, 0);
                }

                return new (new (JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions)), ContentType, PayloadFormatIndicator);
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
