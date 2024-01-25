// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Json
{
    using Furly.Extensions.Serializers;
    using Furly.Exceptions;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    /// <summary>
    /// Json serializer
    /// </summary>
    public class DefaultJsonSerializer : IJsonSerializerSettingsProvider, IJsonSerializer
    {
        /// <inheritdoc/>
        public string MimeType => ContentMimeType.Json;

        /// <inheritdoc/>
        public Encoding ContentEncoding => Encoding.UTF8;

        /// <summary>
        /// Json serializer settings
        /// </summary>
        public JsonSerializerOptions Settings { get; }

        /// <summary>
        /// Create serializer
        /// </summary>
        /// <param name="providers"></param>
        public DefaultJsonSerializer(
            IEnumerable<IJsonSerializerConverterProvider>? providers = null)
        {
            var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    foreach (var converter in provider.GetConverters())
                    {
                        settings.Converters.Add(converter);
                    }
                }
            }
            settings.Converters.Add(new MatrixConverter());
            settings.Converters.Add(new ByteArrayConverter());
            settings.Converters.Add(new XmlElementConverter());
            settings.Converters.Add(new BigIntegerConverter());
            settings.Converters.Add(new JsonVariantConverter(this));
            settings.Converters.Add(new DataContractObjectConverter());
            settings.Converters.Add(new DataContractEnumConverter(
                JsonNamingPolicy.CamelCase, true));
            settings.Converters.Add(new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase, true));
            settings.Converters.Add(new ReadOnlySetConverter());
            settings.NumberHandling =
                JsonNumberHandling.AllowReadingFromString |
                JsonNumberHandling.AllowNamedFloatingPointLiterals;

            settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            settings.PropertyNameCaseInsensitive = true;
            settings.AllowTrailingCommas = true;
            settings.WriteIndented = false;
            settings.DefaultBufferSize = 128;
            settings.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            if (settings.MaxDepth > 64)
            {
                settings.MaxDepth = 64;
            }
            Settings = settings;
        }

        /// <inheritdoc/>
        public object? Deserialize(ReadOnlyMemory<byte> buffer, Type type)
        {
            try
            {
                return JsonSerializer.Deserialize(buffer.Span, type, Settings);
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public async ValueTask<object?> DeserializeAsync(Stream stream, Type type,
            int? sizeHint, CancellationToken ct)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(stream, type, Settings,
                    ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<T?> DeserializeStreamAsync<T>(Stream stream, CancellationToken ct)
        {
            return DeserializeAsyncEnumerableAsync<T, T>(stream, ct);
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<object?> DeserializeStreamAsync(Stream stream,
            Type type, CancellationToken ct)
        {
            var enumerable = GetType().GetMethod(nameof(DeserializeAsyncEnumerableAsync),
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(type, typeof(object)).Invoke(this,
                    new object[] { stream, ct });
            return (IAsyncEnumerable<object?>)enumerable!;
        }

        /// <inheritdoc/>
        public async Task SerializeObjectAsync(Stream stream, object? o, Type? type,
            SerializeOption format, CancellationToken ct)
        {
            try
            {
                var ot = type ?? o?.GetType() ?? typeof(object);
                var settings = Settings;
                if (format == SerializeOption.Indented)
                {
                    settings = new JsonSerializerOptions(Settings)
                    {
                        WriteIndented = true
                    };
                }
                await JsonSerializer.SerializeAsync(stream, o, ot, settings,
                    ct).ConfigureAwait(false);
                return;
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public void SerializeObject(IBufferWriter<byte> buffer, object? o, Type? type,
            SerializeOption format)
        {
            try
            {
                var ot = type ?? o?.GetType() ?? typeof(object);
                var options = format == SerializeOption.Indented ?
                    new JsonWriterOptions() { Indented = true } : default;

                using (var writer = new Utf8JsonWriter(buffer, options))
                {
                    JsonSerializer.Serialize(writer, o, ot, Settings);
                }
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public VariantValue Parse(ReadOnlyMemory<byte> buffer)
        {
            try
            {
                var reader = new Utf8JsonReader(buffer.Span, new JsonReaderOptions
                {
                    MaxDepth = Settings.MaxDepth
                });
                var node = JsonNode.Parse(ref reader, new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return new JsonVariantValue(node, this);
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public VariantValue FromObject(object? o)
        {
            try
            {
                return new JsonVariantValue(this, o);
            }
            catch (JsonException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Helper to enumerate stream of deserialized objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <param name="stream"></param>
        /// <param name="ct"></param>
        /// <exception cref="SerializerException"></exception>
        internal async IAsyncEnumerable<R?> DeserializeAsyncEnumerableAsync<T, R>(Stream stream,
            [EnumeratorCancellation] CancellationToken ct) where T : R
        {
            var enumerator = JsonSerializer.DeserializeAsyncEnumerable<T>(stream, Settings,
                ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    try
                    {
                        var cont = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        if (!cont)
                        {
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        throw new SerializerException(ex.Message, ex);
                    }
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Node wrapper
        /// </summary>
        internal class JsonVariantValue : VariantValue
        {
            /// <summary>
            /// The wrapped node
            /// </summary>
            internal JsonNode? Node { get; set; }

            /// <summary>
            /// Create value
            /// </summary>
            /// <param name="serializer"></param>
            /// <param name="o"></param>
            internal JsonVariantValue(DefaultJsonSerializer serializer, object? o)
            {
                _serializer = serializer;
                Node = FromObject(o);
            }

            /// <summary>
            /// Create value
            /// </summary>
            /// <param name="node"></param>
            /// <param name="serializer"></param>
            /// <param name="parentUpdate"></param>
            internal JsonVariantValue(JsonNode? node, DefaultJsonSerializer serializer,
                Action<JsonNode?>? parentUpdate = null)
            {
                _serializer = serializer;
                Node = node;
                _update = parentUpdate;
            }

            /// <inheritdoc/>
            protected override VariantValueType GetValueType()
            {
                switch (Node)
                {
                    case JsonObject:
                        return VariantValueType.Complex;
                    case JsonArray:
                        return VariantValueType.Values;
                    case JsonValue:
                        return VariantValueType.Primitive;
                    case null:
                        return VariantValueType.Null;
                }
                throw new InvalidOperationException("Node type is unknown");
            }

            /// <inheritdoc/>
            protected override object? GetRawValue()
            {
                if (Node is JsonValue v)
                {
                    var o = v.GetValue<object?>();
                    if (o is JsonElement e)
                    {
                        switch (e.ValueKind)
                        {
                            case JsonValueKind.Number:
                                return v;
                            case JsonValueKind.String:
                                return (string?)v;
                            case JsonValueKind.True:
                                return true;
                            case JsonValueKind.False:
                                return false;
                            case JsonValueKind.Null:
                                return null;
                        }
                    }
                }
                return Node;
            }

            /// <inheritdoc/>
            protected override IEnumerable<string> GetObjectProperties()
            {
                if (Node is JsonObject o)
                {
                    return o.Select(p => p.Key);
                }
                return Enumerable.Empty<string>();
            }

            /// <inheritdoc/>
            protected override IEnumerable<VariantValue> GetArrayElements()
            {
                if (Node is JsonArray array)
                {
                    return array.Select(i => new JsonVariantValue(i, _serializer));
                }
                return Enumerable.Empty<VariantValue>();
            }

            /// <inheritdoc/>
            protected override int GetArrayCount()
            {
                if (Node is JsonArray array)
                {
                    return array.Count;
                }
                return 0;
            }

            /// <inheritdoc/>
            public override VariantValue Copy(bool shallow)
            {
                var node = shallow || Node == null ? Node :
                    JsonNode.Parse(Node.ToJsonString(_serializer.Settings));
                return new JsonVariantValue(node, _serializer, null);
            }

            /// <inheritdoc/>
            public override object? ConvertTo(Type type)
            {
                try
                {
                    return Node.Deserialize(type, _serializer.Settings);
                }
                catch (JsonException ex)
                {
                    if (type == typeof(byte[]) || typeof(IEnumerable<byte>).IsAssignableFrom(type))
                    {
                        return ConvertToByteArray();
                    }
                    throw new SerializerException(ex.Message, ex);
                }
            }

            /// <inheritdoc/>
            protected override StringBuilder AppendTo(StringBuilder builder)
            {
                if (Node is null)
                {
                    return builder.Append("null");
                }
                var relaxed = new JsonSerializerOptions(_serializer.Settings)
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                return builder.Append(Node.ToJsonString(relaxed));
            }

            /// <inheritdoc/>
            public override bool TryGetProperty(string key,
                [NotNullWhen(true)] out VariantValue value)
            {
                if (Node is JsonObject o)
                {
                    var success = o.TryGetPropertyValue(key, out var node);
                    if (success)
                    {
                        value = new JsonVariantValue(node, _serializer, v => o[key] = v);
                        return true;
                    }
                }
                value = new JsonVariantValue(null, _serializer);
                return false;
            }

            /// <inheritdoc/>
            public override bool TryGetElement(int index,
                [NotNullWhen(true)] out VariantValue value)
            {
                if (index >= 0 && Node is JsonArray o && index < o.Count)
                {
                    value = new JsonVariantValue(o[index], _serializer, v => o[index] = v);
                    return true;
                }
                value = new JsonVariantValue(null, _serializer);
                return false;
            }

            /// <inheritdoc/>
            protected override VariantValue AddProperty(string propertyName)
            {
                if (Node is JsonObject o)
                {
                    var child = new JsonVariantValue(null, _serializer, v => o[propertyName] = v);
                    // Add to object
                    o.Add(propertyName, child.Node);
                    return child;
                }
                throw new NotSupportedException("Not an object");
            }

            /// <inheritdoc/>
            public override void AssignValue(object? value)
            {
                if (_update != null)
                {
                    Node = FromObject(value);
                    _update(Node);
                    return;
                }

                switch (Node?.Parent)
                {
                    case JsonObject o:
                        // Part of an object - update object
                        var property = o.FirstOrDefault(p => p.Value == Node);
                        if (property.Value == null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(value), "No parent found");
                        }
                        Node = FromObject(value);
                        o[property.Key] = Node;
                        break;
                    case JsonArray a:
                        // Part of an object - update object
                        for (var i = 0; i < a.Count; i++)
                        {
                            if (a[i] == Node)
                            {
                                Node = FromObject(value);
                                a[i] = Node;
                                return;
                            }
                        }
                        throw new ArgumentOutOfRangeException(nameof(value), "No parent found");
                    default:
                        throw new NotSupportedException("Not an object or array");
                }
            }

            /// <inheritdoc/>
            protected override bool TryEqualsValue(object? o, out bool equality)
            {
                if (o is JsonNode t)
                {
                    equality = DeepEquals(Node, t);
                    if (equality)
                    {
                        return true;
                    }
                    //
                    // We cannot trust equality here fully since our equality is
                    // based on json string equivalence.
                    //
                }
                else if (Node is JsonValue v)
                {
                    if (o is DateTime or DateTimeOffset)
                    {
                        equality = DeepEquals(v, JsonValue.Create(o));
                        return true;
                    }
                }
                return base.TryEqualsValue(o, out equality);
            }

            /// <inheritdoc/>
            protected override bool TryEqualsVariant(VariantValue? v, out bool equality)
            {
                if (v is JsonVariantValue json)
                {
                    equality = DeepEquals(Node, json.Node);
                    if (equality)
                    {
                        return true;
                    }
                    //
                    // We cannot trust equality here fully since our equality is
                    // based on json string equivalence.
                    //
                }
                return base.TryEqualsVariant(v, out equality);
            }

            /// <summary>
            /// Compare tokens in more consistent fashion
            /// </summary>
            /// <param name="t1"></param>
            /// <param name="t2"></param>
            internal bool DeepEquals(JsonNode? t1, JsonNode? t2)
            {
                if (t1 == null || t2 == null)
                {
                    return t1 == t2;
                }
                if (ReferenceEquals(t1, t2))
                {
                    return true;
                }
                if (t1 is JsonObject o1 && t2 is JsonObject o2)
                {
                    // Compare properties in order of key
                    var props1 = o1.OrderBy(k => k.Key)
                        .Select(p => p.Value);
                    var props2 = o2.OrderBy(k => k.Key)
                        .Select(p => p.Value);
                    return props1.SequenceEqual(props2,
                        Compare.Using<JsonNode?>((x, y) => DeepEquals(x, y)));
                }

                if (t1 is JsonArray c1 && t2 is JsonArray c2)
                {
                    // For arrays order is important
                    return c1.SequenceEqual(c2,
                        Compare.Using<JsonNode?>((x, y) => DeepEquals(x, y)));
                }

                if (t1 is JsonValue && t2 is JsonValue)
                {
                    var relaxed = new JsonSerializerOptions(_serializer.Settings)
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    var s1 = t1.ToJsonString(relaxed);
                    var s2 = t2.ToJsonString(relaxed);
                    if (s1 == s2)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Convert to byte array
            /// </summary>
            /// <exception cref="SerializerException"></exception>
            private byte[]? ConvertToByteArray()
            {
                try
                {
                    var array = Node.Deserialize<int[]>(_serializer.Settings);
                    return array?.Select(a => (byte)a).ToArray();
                }
                catch (JsonException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            /// <summary>
            /// Create document from object and rethrow serializer exception
            /// </summary>
            /// <param name="o"></param>
            /// <exception cref="SerializerException"></exception>
            private JsonNode? FromObject(object? o)
            {
                try
                {
                    return JsonSerializer.SerializeToNode(o, _serializer.Settings);
                }
                catch (JsonException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            private readonly Action<JsonNode?>? _update;
            private readonly DefaultJsonSerializer _serializer;
        }

        /// <summary>
        /// Json veriant converter
        /// </summary>
        internal sealed class JsonVariantConverter : JsonConverter<VariantValue>
        {
            /// <summary>
            /// Converter
            /// </summary>
            /// <param name="serializer"></param>
            public JsonVariantConverter(DefaultJsonSerializer serializer)
            {
                _serializer = serializer;
            }

            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(VariantValue).IsAssignableFrom(typeToConvert);
            }

            public override VariantValue? Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                // Read variant from json
                var node = JsonNode.Parse(ref reader, new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (node is null)
                {
                    return null;
                }
                return new JsonVariantValue(node, _serializer);
            }

            public override void Write(Utf8JsonWriter writer,
                VariantValue value, JsonSerializerOptions options)
            {
                if (value is JsonVariantValue packed)
                {
                    if (packed.Node is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        packed.Node.WriteTo(writer, options);
                    }
                }
                else if (value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    if (value.IsNull())
                    {
                        writer.WriteNullValue();
                    }
                    else if (value.IsListOfValues)
                    {
                        writer.WriteStartArray();
                        foreach (var item in value.Values)
                        {
                            JsonSerializer.Serialize(writer, item, options);
                        }
                        writer.WriteEndArray();
                    }
                    else if (value.IsObject)
                    {
                        writer.WriteStartObject();
                        foreach (var key in value.PropertyNames)
                        {
                            var item = value[key];
                            if (item.IsNull() && options.DefaultIgnoreCondition != JsonIgnoreCondition.Never)
                            {
                                break;
                            }
                            writer.WritePropertyName(key);
                            Write(writer, item, options);
                        }
                        writer.WriteEndObject();
                    }
                    else if (value.TryGetValue(out var primitive, CultureInfo.InvariantCulture))
                    {
                        JsonSerializer.Serialize(writer, primitive, options);
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, value.Value, options);
                    }
                }
            }

            private readonly DefaultJsonSerializer _serializer;
        }

        internal sealed class DataContractObjectConverter : JsonConverterFactory
        {
            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                var dca = typeToConvert.GetCustomAttribute<DataContractAttribute>(true);
                if (dca == null)
                {
                    return false;
                }
                var constructors = typeToConvert.GetConstructors();
                if (constructors.Length != 0 && !constructors
                    .Any(c => c.GetParameters().Length == 0))
                {
                    // No support for parameter based construction at this point.
                    return false;
                }
                // If data member attribute is being used
                return typeToConvert.GetProperties()
                    .Any(p => p.CanWrite && !p.IsSpecialName &&
                        p.GetCustomAttribute<DataMemberAttribute>() != null);
            }

            /// <inheritdoc/>
            public override JsonConverter? CreateConverter(Type typeToConvert,
                JsonSerializerOptions options)
            {
                var ct = typeof(DataContractObjectConverterOfT<>)
                    .MakeGenericType(typeToConvert);
                return (JsonConverter?)Activator.CreateInstance(ct, Array.Empty<object>());
            }

            /// <summary>
            /// Actual converter of T
            /// </summary>
            /// <typeparam name="T"></typeparam>
            public class DataContractObjectConverterOfT<T> : JsonConverter<T>
                where T : new()
            {
                /// <inheritdoc/>
                public override T Read(ref Utf8JsonReader reader, Type typeToConvert,
                    JsonSerializerOptions options)
                {
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw new JsonException();
                    }
                    var o = new T();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            return o;
                        }
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var propertyName = reader.GetString();
                            if (propertyName == null)
                            {
                                throw new JsonException();
                            }
                            reader.Read();
                            ReadFn? setter;
                            if (options.PropertyNameCaseInsensitive)
                            {
                                if (!kReadersInsensitive.TryGetValue(
                                    propertyName.ToUpperInvariant(), out setter))
                                {
                                    throw new JsonException(
                                       $"No case insensitive reader for {propertyName}");
                                }
                            }
                            else if (!kReaders.TryGetValue(propertyName, out setter))
                            {
                                throw new JsonException($"No reader for {propertyName}");
                            }
                            setter(ref reader, o, options);
                        }
                    }
                    return o;
                }

                /// <inheritdoc/>
                public override void Write(Utf8JsonWriter writer, T value,
                    JsonSerializerOptions options)
                {
                    writer.WriteStartObject();
                    foreach (var write in kWriters)
                    {
                        write(value, writer, options);
                    }
                    writer.WriteEndObject();
                }

                private delegate void WriteFn(object? o, Utf8JsonWriter writer,
                    JsonSerializerOptions options);

                private delegate void ReadFn(ref Utf8JsonReader reader, object? o,
                    JsonSerializerOptions options);

                /// <summary>
                /// Gather type information
                /// </summary>
                static DataContractObjectConverterOfT()
                {
                    kReaders = typeof(T).GetProperties()
                        .Where(p => p.CanWrite && !p.IsSpecialName &&
                            p.GetCustomAttribute<DataMemberAttribute>() != null)
                        .Select(p =>
                        {
                            var dma = p.GetCustomAttribute<DataMemberAttribute>();
                            var name = dma?.Name ?? p.Name;
                            var typeToRead = p.GetSetMethod()?
                                .GetParameters()[0].ParameterType;
                            void Read(ref Utf8JsonReader reader, object? o,
                                JsonSerializerOptions options)
                            {
                                var typeToRead = p.GetSetMethod()?
                                    .GetParameters()[0].ParameterType;
                                var v = JsonSerializer.Deserialize(
                                    ref reader, typeToRead ?? typeof(object), options);
                                try
                                {
                                    p.SetValue(o, v);
                                }
                                catch (Exception ex)
                                {
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                                    throw new JsonException(ex.Message, ex);
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
                                }
                            }
                            ReadFn read = Read;
                            return (name, read);
                        })
                        .ToDictionary(p => p.name, v => v.read);

                    kReadersInsensitive = kReaders
                        .ToDictionary(p => p.Key.ToUpperInvariant(), kv => kv.Value);

                    kWriters = typeof(T).GetProperties()
                        .Where(p => p.CanRead && !p.IsSpecialName &&
                            p.GetCustomAttribute<DataMemberAttribute>() != null)
                        .Select(p =>
                        {
                            var dma = p.GetCustomAttribute<DataMemberAttribute>();
                            var name = JsonEncodedText.Encode(dma?.Name ?? p.Name);
                            var emitDefault = dma?.EmitDefaultValue != false;
                            var typeToWrite = p.GetGetMethod()?.ReturnType;
                            var defaultValue = typeToWrite?.IsValueType ?? false ?
                                Activator.CreateInstance(typeToWrite) : null;
                            void Write(object? o, Utf8JsonWriter writer,
                                JsonSerializerOptions options)
                            {
                                object? v;
                                try
                                {
                                    v = p.GetValue(o);
                                }
                                catch
                                {
                                    v = defaultValue;
                                }
                                if (emitDefault || !IsEqual(defaultValue, v))
                                {
                                    writer.WritePropertyName(name);
                                    JsonSerializer.Serialize(writer, v,
                                        typeToWrite ?? v?.GetType() ?? typeof(object),
                                        options);
                                }
                            }
                            return (WriteFn)Write;
                        })
                        .Where(p => p != null)
                        .ToList();
                }

                private static bool IsEqual(object? defaultValue, object? v)
                {
                    if (v == defaultValue)
                    {
                        return true;
                    }
                    if (v is null || defaultValue is null)
                    {
                        return false;
                    }
                    return v.Equals(defaultValue);
                }

                private static readonly Dictionary<string, ReadFn> kReaders;
                private static readonly Dictionary<string, ReadFn> kReadersInsensitive;
                private static readonly List<WriteFn> kWriters;
            }
        }

        internal sealed class DataContractEnumConverter : JsonConverterFactory
        {
            /// <summary>
            /// Create converter
            /// </summary>
            /// <param name="namingPolicy"></param>
            /// <param name="allowIntValues"></param>
            public DataContractEnumConverter(JsonNamingPolicy namingPolicy,
                bool allowIntValues)
            {
                _namingPolicy = namingPolicy;
                _fallback = new JsonStringEnumConverter(namingPolicy, allowIntValues);
            }

            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                if (!typeToConvert.IsEnum)
                {
                    return false;
                }
                var dca = typeToConvert.GetCustomAttribute<DataContractAttribute>(true);
                if (dca == null)
                {
                    return false;
                }
                // If enum member attribute used
                return typeToConvert.GetMembers()
                    .Any(p => p.GetCustomAttribute<EnumMemberAttribute>() != null);
            }

            /// <inheritdoc/>
            public override JsonConverter? CreateConverter(Type typeToConvert,
                JsonSerializerOptions options)
            {
                var ct = typeof(DataContractEnumConverterOfT<>)
                    .MakeGenericType(typeToConvert);
                return (JsonConverter?)Activator.CreateInstance(ct, new object[] {
                    _fallback.CreateConverter(typeToConvert, options),
                    this
                });
            }

            /// <summary>
            /// Actual converter of T
            /// </summary>
            /// <typeparam name="T"></typeparam>
            public class DataContractEnumConverterOfT<T> : JsonConverter<T>
                where T : struct, Enum
            {
                /// <summary>
                /// Create converter
                /// </summary>
                /// <param name="fallback"></param>
                /// <param name="outer"></param>
                public DataContractEnumConverterOfT(JsonConverter<T>? fallback,
                    DataContractEnumConverter outer)
                {
                    _fallback = fallback;
                    _outer = outer;
                }

                /// <inheritdoc/>
                public override T Read(ref Utf8JsonReader reader, Type typeToConvert,
                    JsonSerializerOptions options)
                {
                    var token = reader.TokenType;
                    if (token == JsonTokenType.String)
                    {
                        var enumString = FormatStringToEnumValue(reader.GetString());
                        if (enumString == null)
                        {
                            throw new JsonException();
                        }
                        if (!Enum.TryParse<T>(enumString, ignoreCase: true, out var value))
                        {
                            throw new JsonException();
                        }
                        return value;
                    }
                    if (_fallback == null)
                    {
                        throw new JsonException("Not supported");
                    }
                    return _fallback.Read(ref reader, typeToConvert, options);
                }

                /// <inheritdoc/>
                public override void Write(Utf8JsonWriter writer, T value,
                    JsonSerializerOptions options)
                {
                    var key = ConvertToUInt64(value);
                    if (kCache.TryGetValue(key, out var formatted))
                    {
                        writer.WriteStringValue(formatted);
                        return;
                    }

                    var enumString = FormatEnumValueToString(value.ToString(), options);
                    if (enumString != null)
                    {
                        formatted = JsonEncodedText.Encode(enumString, options.Encoder);
                        writer.WriteStringValue(formatted);
                        kCache.TryAdd(key, formatted);
                        return;
                    }

                    if (_fallback == null)
                    {
                        throw new JsonException("Not supported");
                    }
                    _fallback.Write(writer, value, options);
                }

                private static string? FormatStringToEnumValue(string? value)
                {
                    if (value == null)
                    {
                        return null;
                    }
                    if (!value.Contains(kSeperator, StringComparison.Ordinal))
                    {
                        return Convert(value);
                    }
                    var enumValues = value.Split(kSeperator, StringSplitOptions.RemoveEmptyEntries);
                    for (var i = 0; i < enumValues.Length; i++)
                    {
                        enumValues[i] = Convert(enumValues[i]);
                    }
                    return string.Join(kSeperator, enumValues);
                    static string Convert(string value)
                    {
                        if (kValueToMember.TryGetValue(value.ToUpperInvariant(), out var actual))
                        {
                            value = actual;
                        }
                        return value;
                    }
                }

                private string? FormatEnumValueToString(string? value, JsonSerializerOptions options)
                {
                    if (value == null)
                    {
                        return null;
                    }
                    var namingPolicy = _outer._namingPolicy ?? options.PropertyNamingPolicy;
                    if (!value.Contains(kSeperator, StringComparison.Ordinal))
                    {
                        return Convert(value, namingPolicy);
                    }
                    var enumValues = value.Split(kSeperator, StringSplitOptions.RemoveEmptyEntries);
                    for (var i = 0; i < enumValues.Length; i++)
                    {
                        enumValues[i] = Convert(enumValues[i], namingPolicy);
                    }
                    return string.Join(kSeperator, enumValues);
                    static string Convert(string value, JsonNamingPolicy? policy)
                    {
                        if (kMemberToValue.TryGetValue(value, out var actual))
                        {
                            value = actual;
                        }
                        return policy != null ? policy.ConvertName(value) : value;
                    }
                }

                /// <summary>
                /// Gather type information
                /// </summary>
                static DataContractEnumConverterOfT()
                {
                    kTypeCode = Type.GetTypeCode(typeof(T));
                    kMemberToValue = typeof(T).GetMembers()
                        .Where(p => p.GetCustomAttribute<EnumMemberAttribute>() != null)
                        .ToDictionary(m => m.Name,
                            p => p.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? p.Name);
                    kValueToMember = kMemberToValue
                        .ToDictionary(k => k.Value.ToUpperInvariant(), v => v.Key);
                }

                private static ulong ConvertToUInt64(object value)
                {
                    System.Diagnostics.Debug.Assert(value is T);
                    return kTypeCode switch
                    {
                        TypeCode.Int32 => (ulong)(int)value,
                        TypeCode.UInt32 => (uint)value,
                        TypeCode.UInt64 => (ulong)value,
                        TypeCode.Int64 => (ulong)(long)value,
                        TypeCode.SByte => (ulong)(sbyte)value,
                        TypeCode.Byte => (byte)value,
                        TypeCode.Int16 => (ulong)(short)value,
                        TypeCode.UInt16 => (ushort)value,
                        _ => throw new InvalidOperationException(),
                    };
                }

                private const string kSeperator = ", ";
                private static readonly Dictionary<string, string> kValueToMember;
                private static readonly Dictionary<string, string> kMemberToValue;
                private static readonly ConcurrentDictionary<ulong, JsonEncodedText> kCache = new();
                private static readonly TypeCode kTypeCode;
                private readonly JsonConverter<T>? _fallback;
                private readonly DataContractEnumConverter _outer;
            }
            private readonly JsonStringEnumConverter _fallback;
            private readonly JsonNamingPolicy _namingPolicy;
        }

        /// <summary>
        /// Readonly set converter
        /// </summary>
        internal sealed class ReadOnlySetConverter : JsonConverterFactory
        {
            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                var type = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlySet<>));
                return type != null;
            }

            /// <inheritdoc/>
            public override JsonConverter? CreateConverter(Type typeToConvert,
                JsonSerializerOptions options)
            {
                var type = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlySet<>));
                System.Diagnostics.Debug.Assert(type != null);
                var ct = typeof(ReadOnlySetConverterOfT<,>)
                    .MakeGenericType(typeToConvert, type.GetGenericArguments()[0]);
                return (JsonConverter?)Activator.CreateInstance(ct, Array.Empty<object>());
            }

            /// <summary>
            /// Actual converter of T
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <typeparam name="TElement"></typeparam>
            public class ReadOnlySetConverterOfT<T, TElement> : JsonConverter<T?>
            {
                /// <inheritdoc/>
                public override void Write(Utf8JsonWriter writer, T? value,
                    JsonSerializerOptions options)
                {
                    JsonSerializer.Serialize(writer, (IEnumerable<TElement?>?)value, options);
                }

                /// <inheritdoc/>
                public override T? Read(ref Utf8JsonReader reader, Type typeToConvert,
                    JsonSerializerOptions options)
                {
                    var set = JsonSerializer.Deserialize<TElement?[]?>(ref reader, options);
                    if (set != null)
                    {
                        return (T?)(IReadOnlySet<TElement?>?)new HashSet<TElement?>(set);
                    }
                    return default;
                }
            }
        }

        /// <summary>
        /// Byte array converter allowing list of integers
        /// </summary>
        internal sealed class ByteArrayConverter : JsonConverter<byte[]>
        {
            /// <inheritdoc/>
            public override byte[]? Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = JsonSerializer.Deserialize<List<byte>>(ref reader, options);
                    return list?.ToArray();
                }
                return reader.GetBytesFromBase64();
            }

            /// <inheritdoc/>
            public override void Write(Utf8JsonWriter writer,
                byte[]? value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteBase64StringValue(value);
                }
            }
        }

        /// <summary>
        /// Byte array converter allowing list of integers
        /// </summary>
        internal sealed class XmlElementConverter : JsonConverter<XmlElement>
        {
            /// <inheritdoc/>
            public override XmlElement? Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }
                if (reader.TokenType == JsonTokenType.String)
                {
                    var encoded = reader.GetBytesFromBase64();
                    var xml = Encoding.UTF8.GetString(encoded);
                    if (xml == null)
                    {
                        return null;
                    }
                    var doc = new XmlDocument();
                    doc.LoadXml(xml);
                    return doc.DocumentElement;
                }
                throw new JsonException();
            }

            /// <inheritdoc/>
            public override void Write(Utf8JsonWriter writer,
                XmlElement? value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    var encoded = Encoding.UTF8.GetBytes(value.OuterXml);
                    writer.WriteBase64StringValue(encoded);
                }
            }
        }

        /// <summary>
        /// Big integer converter
        /// </summary>
        internal sealed class BigIntegerConverter : JsonConverter<BigInteger>
        {
            /// <inheritdoc/>
            public override BigInteger Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType is not JsonTokenType.Number and
                    not JsonTokenType.String)
                {
                    throw new JsonException();
                }
                using var doc = JsonDocument.ParseValue(ref reader);
                var txt = doc.RootElement.GetRawText();
                if (reader.TokenType == JsonTokenType.String &&
                    txt.Length >= 2 && txt[0] == '"' && txt[^1] == '"')
                {
                    // Trim quotes
                    txt = txt[1..^1].Trim();
                }
                return BigInteger.Parse(txt, NumberFormatInfo.InvariantInfo);
            }

            /// <inheritdoc/>
            public override void Write(Utf8JsonWriter writer, BigInteger value,
                JsonSerializerOptions options)
            {
                var s = value.ToString(NumberFormatInfo.InvariantInfo);
                using var doc = JsonDocument.Parse(s);
                doc.WriteTo(writer);
            }
        }

        /// <summary>
        /// Matrix converter
        /// </summary>
        internal sealed class MatrixConverter : JsonConverterFactory
        {
            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                if (typeToConvert.IsArray && typeToConvert.GetArrayRank() > 1)
                {
                    return true;
                }
                return false;
            }

            /// <inheritdoc/>
            public override JsonConverter? CreateConverter(Type typeToConvert,
                JsonSerializerOptions options)
            {
                var ct = typeof(MatrixConverterOfT<,>).MakeGenericType(
                    typeToConvert, typeToConvert.GetElementType()!);
                return (JsonConverter?)Activator.CreateInstance(ct, Array.Empty<object>());
            }

            /// <summary>
            /// Actual converter of T where T is the array and E is the element type
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <typeparam name="E"></typeparam>
            public class MatrixConverterOfT<T, E> : JsonConverter<T?> where T : class
            {
                /// <inheritdoc/>
                public override T? Read(ref Utf8JsonReader reader, Type typeToConvert,
                    JsonSerializerOptions options)
                {
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        // Expected to be at beginning of array or null
                        throw new JsonException("Expected beginning of matrix array.");
                    }

                    var lengths = new int[typeToConvert.GetArrayRank()];
                    var slices = ReadDimension(0, ref reader, typeToConvert, lengths, options);
                    if (slices is not Array from)
                    {
                        throw new JsonException();
                    }
                    var to = Array.CreateInstance(typeof(E), lengths);
                    Array.Clear(lengths);
                    CopyTo(from, to, lengths, 0);
                    return to as T;
                }

                /// <summary>
                /// Read array dimensions
                /// </summary>
                /// <param name="dimension"></param>
                /// <param name="reader"></param>
                /// <param name="typeToConvert"></param>
                /// <param name="lengths"></param>
                /// <param name="options"></param>
                /// <exception cref="JsonException"></exception>
                private static object? ReadDimension(int dimension,
                    ref Utf8JsonReader reader, Type typeToConvert, int[] lengths,
                    JsonSerializerOptions options)
                {
                    if (reader.TokenType == JsonTokenType.Null)
                    {
                        return null;
                    }

                    if (dimension == lengths.Length - 1)
                    {
                        // Last dimension - read the array slice
                        var result = JsonSerializer.Deserialize(ref reader,
                            typeof(E).MakeArrayType(), options);
                        if (result is E[] element && element.Length > lengths[dimension])
                        {
                            lengths[dimension] = element.Length;
                        }
                        return result;
                    }

                    var list = new List<object?>();
                    while (true)
                    {
                        if (!reader.Read())
                        {
                            throw new JsonException("Failed to read");
                        }
                        if (reader.TokenType == JsonTokenType.EndArray)
                        {
                            // we have read the last item of the array
                            break;
                        }

                        // Now at start of array of next dimension
                        var result = ReadDimension(dimension + 1, ref reader,
                            typeToConvert, lengths, options);

                        list.Add(result);
                    }
                    if (list.Count > lengths[dimension])
                    {
                        lengths[dimension] = list.Count;
                    }
                    return list.ToArray(); // Slice
                }

                /// <inheritdoc/>
                public override void Write(Utf8JsonWriter writer, T? value,
                    JsonSerializerOptions options)
                {
                    if (value is Array a)
                    {
                        var indices = new int[a.Rank];
                        WriteDimension(0, writer, a, indices, options);
                    }
                    else
                    {
                        writer.WriteNullValue();
                    }
                }

                /// <summary>
                /// Write array
                /// </summary>
                /// <param name="dimension"></param>
                /// <param name="writer"></param>
                /// <param name="array"></param>
                /// <param name="indices"></param>
                /// <param name="options"></param>
                private static void WriteDimension(int dimension, Utf8JsonWriter writer,
                    Array array, int[] indices, JsonSerializerOptions options)
                {
                    if (dimension == indices.Length - 1)
                    {
                        var slice = Slice(array, indices).ToArray();
                        JsonSerializer.Serialize(writer, slice, options);
                    }
                    else
                    {
                        writer.WriteStartArray();
                        for (var index = 0; index < array.GetLength(dimension); index++)
                        {
                            indices[dimension] = index;
                            WriteDimension(dimension + 1, writer, array, indices, options);
                        }
                        writer.WriteEndArray();
                    }
                    static IEnumerable<E?> Slice(Array array, int[] indices)
                    {
                        for (var index = 0; index < array.GetLength(indices.Length - 1); index++)
                        {
                            indices[^1] = index;
                            yield return (E?)array.GetValue(indices);
                        }
                    }
                }

                /// <summary>
                /// Copies slices to multidimensional array
                /// </summary>
                /// <param name="slice"></param>
                /// <param name="array"></param>
                /// <param name="indices"></param>
                /// <param name="dimension"></param>
                /// <exception cref="JsonException"></exception>
                private static void CopyTo(Array slice, Array array, int[] indices, int dimension)
                {
                    indices[dimension] = 0;
                    foreach (var item in slice)
                    {
                        if (item is Array inner)
                        {
                            CopyTo(inner, array, indices, dimension + 1);
                        }
                        else
                        {
                            if (dimension != indices.Length - 1)
                            {
                                throw new JsonException();
                            }
                            array.SetValue(item, indices);
                        }
                        indices[dimension]++;
                    }
                }
            }
        }
    }
}
