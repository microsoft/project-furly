// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Newtonsoft
{
    using Furly.Extensions.Serializers;
    using Furly.Exceptions;
    using global::Newtonsoft.Json;
    using global::Newtonsoft.Json.Converters;
    using global::Newtonsoft.Json.Linq;
    using global::Newtonsoft.Json.Serialization;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Newtonsoft json serializer
    /// </summary>
    public class NewtonsoftJsonSerializer : INewtonsoftSerializerSettingsProvider,
        IJsonSerializer
    {
        /// <inheritdoc/>
        public string MimeType => ContentMimeType.Json;

        /// <inheritdoc/>
        public Encoding ContentEncoding => Encoding.UTF8;

        /// <summary>
        /// Json serializer settings
        /// </summary>
        public JsonSerializerSettings Settings { get; }

        /// <summary>
        /// Create serializer
        /// </summary>
        /// <param name="providers"></param>
        public NewtonsoftJsonSerializer(
            IEnumerable<INewtonsoftJsonConverterProvider>? providers = null)
        {
            var settings = new JsonSerializerSettings();
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
            settings.ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseDictionaryKeys()
            };
            settings.Converters.Add(new ReadOnlyBufferConverter());
            settings.Converters.Add(new ReadOnlySetConverter());
            settings.Converters.Add(new XmlElementConverter());
            settings.Converters.Add(new JsonVariantConverter(this));
            settings.Converters.Add(new AsyncEnumerableConverter());
            settings.Converters.Add(new StringEnumConverter
            {
                AllowIntegerValues = true,
                NamingStrategy = new CamelCaseNamingStrategy()
            });
            settings.FloatFormatHandling = FloatFormatHandling.String;
            settings.FloatParseHandling = FloatParseHandling.Double;
            settings.DateParseHandling = DateParseHandling.DateTime;
            settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Error;
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
                var jsonSerializer = JsonSerializer.CreateDefault(Settings);
                using (var stream = new MemoryStream(buffer.ToArray()))
                using (var reader = new StreamReader(stream, ContentEncoding))
                {
                    return jsonSerializer.Deserialize(reader, type);
                }
            }
            catch (JsonReaderException ex)
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
                var jsonSerializer = JsonSerializer.CreateDefault(Settings);
                using (var reader = new StreamReader(stream, ContentEncoding))
                {
                    var jsonReader = new JsonTextReader(reader);
                    await using (jsonReader.ConfigureAwait(false))
                    {
                        jsonReader.FloatParseHandling = Settings.FloatParseHandling;
                        jsonReader.DateParseHandling = Settings.DateParseHandling;
                        jsonReader.DateTimeZoneHandling = Settings.DateTimeZoneHandling;
                        jsonReader.MaxDepth = Settings.MaxDepth;

                        try
                        {
                            var token = await JToken.LoadAsync(jsonReader, ct).ConfigureAwait(false);
                            return token.ToObject(type, jsonSerializer);
                        }
                        finally
                        {
                            while (await jsonReader.ReadAsync(ct).ConfigureAwait(false))
                            {
                                // Read to end or throw
                            }
                        }
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<T?> DeserializeStreamAsync<T>(Stream stream,
            [EnumeratorCancellation] CancellationToken ct)
        {
            T[]? array;
            try
            {
                array = (T[]?)await DeserializeAsync(stream, typeof(T[]), null,
                    ct).ConfigureAwait(false);
            }
            catch (JsonReaderException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
            if (array != null)
            {
                foreach (var item in array)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<object?> DeserializeStreamAsync(Stream stream,
            Type type, [EnumeratorCancellation] CancellationToken ct)
        {
            Array? array;
            try
            {
                array = await DeserializeAsync(stream, type.MakeArrayType(), null,
                    ct).ConfigureAwait(false) as Array;
            }
            catch (JsonReaderException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
            if (array != null)
            {
                foreach (var item in array)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc/>
        public async Task SerializeObjectAsync(Stream stream, object? o, Type? type,
            SerializeOption format, CancellationToken ct)
        {
            try
            {
                var jsonSerializer = JsonSerializer.CreateDefault(Settings);
                jsonSerializer.Formatting = format == SerializeOption.Indented ?
                    Formatting.Indented :
                    Formatting.None;
                var writer = new StreamWriter(stream, leaveOpen: true);
                await using (writer.ConfigureAwait(false))
                {
                    var jsonWriter = new JsonTextWriter(writer);
                    await using (jsonWriter.ConfigureAwait(false))
                    {
                        jsonSerializer.Serialize(jsonWriter, o, type);
                    }
                }
            }
            catch (JsonReaderException ex)
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
                var jsonSerializer = JsonSerializer.CreateDefault(Settings);
                jsonSerializer.Formatting = format == SerializeOption.Indented ?
                    Formatting.Indented :
                    Formatting.None;
                using (var stream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(stream, leaveOpen: true))
                    {
                        using var jsonWriter = new JsonTextWriter(writer);
                        jsonSerializer.Serialize(jsonWriter, o, type);
                    }
                    var written = stream.ToArray();
                    buffer.Write(written);
                }
            }
            catch (JsonReaderException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public VariantValue Parse(ReadOnlyMemory<byte> buffer)
        {
            try
            {
                using (var stream = new MemoryStream(buffer.ToArray()))
                using (var reader = new StreamReader(stream, ContentEncoding))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    jsonReader.FloatParseHandling = Settings.FloatParseHandling;
                    jsonReader.DateParseHandling = Settings.DateParseHandling;
                    jsonReader.DateTimeZoneHandling = Settings.DateTimeZoneHandling;
                    jsonReader.MaxDepth = Settings.MaxDepth;

                    var token = JToken.Load(jsonReader);

                    while (jsonReader.Read())
                    {
                        // Read to end or throw
                    }
                    return new JsonVariantValue(token, this);
                }
            }
            catch (JsonReaderException ex)
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
            catch (JsonReaderException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Token wrapper
        /// </summary>
        internal class JsonVariantValue : VariantValue
        {
            /// <summary>
            /// The wrapped token
            /// </summary>
            internal JToken Token { get; set; }

            /// <summary>
            /// Create value
            /// </summary>
            /// <param name="serializer"></param>
            /// <param name="o"></param>
            internal JsonVariantValue(NewtonsoftJsonSerializer serializer, object? o)
            {
                _serializer = serializer;
                Token = o == null ? JValue.CreateNull() : FromObject(o);
            }

            /// <summary>
            /// Create value
            /// </summary>
            /// <param name="token"></param>
            /// <param name="serializer"></param>
            internal JsonVariantValue(JToken? token, NewtonsoftJsonSerializer serializer)
            {
                _serializer = serializer;
                Token = token ?? JValue.CreateNull();
            }

            /// <inheritdoc/>
            protected override VariantValueType GetValueType()
            {
                switch (Token.Type)
                {
                    case JTokenType.Object:
                        return VariantValueType.Complex;
                    case JTokenType.Array:
                        return VariantValueType.Values;
                    case JTokenType.None:
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                    case JTokenType.Constructor:
                    case JTokenType.Property:
                    case JTokenType.Comment:
                        return VariantValueType.Null;
                    default:
                        return VariantValueType.Primitive;
                }
            }

            /// <inheritdoc/>
            protected override object? GetRawValue()
            {
                if (Token is JValue v)
                {
                    if (v.Value is Uri u)
                    {
                        return u.ToString();
                    }
                    return v.Value;
                }
                return Token;
            }

            /// <inheritdoc/>
            protected override IEnumerable<string> GetObjectProperties()
            {
                if (Token is JObject o)
                {
                    return o.Properties().Select(p => p.Name);
                }
                return Enumerable.Empty<string>();
            }

            /// <inheritdoc/>
            protected override IEnumerable<VariantValue> GetArrayElements()
            {
                if (Token is JArray array)
                {
                    return array.Select(i => new JsonVariantValue(i, _serializer));
                }
                return Enumerable.Empty<VariantValue>();
            }

            /// <inheritdoc/>
            protected override int GetArrayCount()
            {
                if (Token is JArray array)
                {
                    return array.Count;
                }
                return 0;
            }

            /// <inheritdoc/>
            public override VariantValue Copy(bool shallow)
            {
                return new JsonVariantValue(shallow ? Token :
                    Token.DeepClone(), _serializer);
            }

            /// <inheritdoc/>
            public override object? ConvertTo(Type type)
            {
                try
                {
                    return Token.ToObject(type,
                        JsonSerializer.CreateDefault(_serializer.Settings));
                }
                catch (JsonReaderException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            /// <inheritdoc/>
            protected override StringBuilder AppendTo(StringBuilder builder)
            {
                return builder.Append(Token.ToString(Formatting.None,
                    _serializer.Settings.Converters.ToArray()));
            }

            /// <inheritdoc/>
            public override bool TryGetProperty(string key, out VariantValue value)
            {
                if (Token is JObject o)
                {
                    var success = o.TryGetValue(key,
                        StringComparison.InvariantCultureIgnoreCase, out var token);
                    if (success)
                    {
                        value = new JsonVariantValue(token, _serializer);
                        return true;
                    }
                }
                value = new JsonVariantValue(null, _serializer);
                return false;
            }

            /// <inheritdoc/>
            public override bool TryGetElement(int index, out VariantValue value)
            {
                if (index >= 0 && Token is JArray o && index < o.Count)
                {
                    value = new JsonVariantValue(o[index], _serializer);
                    return true;
                }
                value = new JsonVariantValue(null, _serializer);
                return false;
            }

            /// <inheritdoc/>
            protected override VariantValue AddProperty(string propertyName)
            {
                if (Token is JObject o)
                {
                    var child = new JsonVariantValue(null, _serializer);
                    // Add to object
                    o.Add(propertyName, child.Token);
                    return child;
                }
                throw new NotSupportedException("Not an object");
            }

            /// <inheritdoc/>
            public override void AssignValue(object? value)
            {
                switch (Token.Parent)
                {
                    case JObject o:
                        // Part of an object - update object
                        var property = o.Properties().FirstOrDefault(p => p.Value == Token);
                        if (property == null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(value), "No parent found");
                        }
                        Token = FromObject(value);
                        property.Value = Token;
                        break;
                    case JArray a:
                        // Part of an object - update object
                        for (var i = 0; i < a.Count; i++)
                        {
                            if (a[i] == Token)
                            {
                                Token = FromObject(value);
                                a[i] = Token;
                                return;
                            }
                        }
                        throw new ArgumentOutOfRangeException(nameof(value), "No parent found");
                    case JProperty p:
                        Token = FromObject(value);
                        p.Value = Token;
                        break;
                    default:
                        throw new NotSupportedException("Not an object or array");
                }
            }

            /// <inheritdoc/>
            protected override bool TryEqualsValue(object? o, out bool equality)
            {
                if (o is JToken t)
                {
                    equality = DeepEquals(Token, t);
                    return true;
                }
                return base.TryEqualsValue(o, out equality);
            }

            /// <inheritdoc/>
            protected override bool TryEqualsVariant(VariantValue? v, out bool equality)
            {
                if (v is JsonVariantValue json)
                {
                    equality = DeepEquals(Token, json.Token);
                    return true;
                }
                return base.TryEqualsVariant(v, out equality);
            }

            /// <inheritdoc/>
            protected override bool TryCompareToValue(object? obj, out int result)
            {
                if (Token is JValue v1 && obj is JValue v2)
                {
                    result = v1.CompareTo(v2);
                    return true;
                }
                return base.TryCompareToValue(obj, out result);
            }

            /// <inheritdoc/>
            protected override bool TryCompareToVariantValue(VariantValue? v, out int result)
            {
                if (v is JsonVariantValue json)
                {
                    return TryCompareToValue(json.Token, out result);
                }
                return base.TryCompareToVariantValue(v, out result);
            }

            /// <summary>
            /// Compare tokens in more consistent fashion
            /// </summary>
            /// <param name="t1"></param>
            /// <param name="t2"></param>
            internal bool DeepEquals(JToken? t1, JToken? t2)
            {
                if (t1 == null || t2 == null)
                {
                    return t1 == t2;
                }
                if (ReferenceEquals(t1, t2))
                {
                    return true;
                }
                if (t1 is JObject o1 && t2 is JObject o2)
                {
                    // Compare properties in order of key
                    var props1 = o1.Properties().OrderBy(k => k.Name)
                        .Select(p => p.Value);
                    var props2 = o2.Properties().OrderBy(k => k.Name)
                        .Select(p => p.Value);
                    return props1.SequenceEqual(props2,
                        Compare.Using<JToken>((x, y) => DeepEquals(x, y)));
                }
                if (t1 is JContainer c1 && t2 is JContainer c2)
                {
                    // For all other containers - order is important
                    return c1.Children().SequenceEqual(c2.Children(),
                        Compare.Using<JToken>((x, y) => DeepEquals(x, y)));
                }
                if (t1 is JValue v1 && t2 is JValue v2)
                {
                    if (v1.Equals(v2))
                    {
                        return true;
                    }
                    var s1 = t1.ToString(Formatting.None,
                        _serializer.Settings.Converters.ToArray());
                    var s2 = t2.ToString(Formatting.None,
                        _serializer.Settings.Converters.ToArray());
                    if (s1 == s2)
                    {
                        return true;
                    }
                    try
                    {
                        // If different types use compare which is less strict
                        return v1.CompareTo(v2) == 0;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }

            /// <summary>
            /// Create token from object and rethrow serializer exception
            /// </summary>
            /// <param name="o"></param>
            /// <exception cref="SerializerException"></exception>
            private JToken FromObject(object? o)
            {
                try
                {
                    return o == null ? JValue.CreateNull() : JToken.FromObject(o,
                        JsonSerializer.CreateDefault(_serializer.Settings));
                }
                catch (JsonReaderException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            private readonly NewtonsoftJsonSerializer _serializer;
        }

        /// <summary>
        /// Convert async enumerable
        /// </summary>
        internal sealed class AsyncEnumerableConverter : JsonConverter
        {
            /// <inheritdoc/>
            public override bool CanRead => true;

            /// <inheritdoc/>
            public override bool CanWrite => true;

            /// <inheritdoc/>
            public override bool CanConvert(Type objectType)
            {
                return objectType.GetCompatibleGenericInterface(
                    typeof(IAsyncEnumerable<>)) != null;
            }

            /// <inheritdoc/>
            public override object? ReadJson(JsonReader reader, Type objectType,
                object? existingValue, JsonSerializer serializer)
            {
                var elementType = objectType.GetGenericArguments()[0];
                return GetType().GetMethod(nameof(ReadAsync))!
                    .MakeGenericMethod(elementType)
                    .Invoke(null, new object[] { reader, serializer });
            }

            /// <summary>
            /// Read enumerable
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="reader"></param>
            /// <param name="serializer"></param>
            public static IAsyncEnumerable<T?>? ReadAsync<T>(JsonReader reader,
                JsonSerializer serializer)
            {
                return serializer.Deserialize<IEnumerable<T?>>(reader)?.ToAsyncEnumerable();
            }

            /// <inheritdoc/>
            public override void WriteJson(JsonWriter writer, object? value,
                JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }
                var elementType = value.GetType().GetGenericArguments()[0];
                typeof(AsyncEnumerableConverter).GetMethod(nameof(Write))!
                    .MakeGenericMethod(elementType)
                    .Invoke(null, new object?[] { writer, value, serializer });
            }

            /// <summary>
            /// Write enumerable
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="writer"></param>
            /// <param name="value"></param>
            /// <param name="serializer"></param>
            public static void Write<T>(JsonWriter writer,
                IAsyncEnumerable<T?>? value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value?.ToEnumerable().ToList());
            }
        }

        /// <summary>
        /// Readonly buffer converter
        /// </summary>
        internal sealed class ReadOnlyBufferConverter : JsonConverter
        {
            /// <inheritdoc/>
            public override bool CanRead => true;

            /// <inheritdoc/>
            public override bool CanWrite => true;

            /// <inheritdoc/>
            public override bool CanConvert(Type objectType)
            {
                if (objectType == typeof(sbyte[]))
                {
                    return false;
                }
                if (!typeof(IReadOnlyCollection<byte>).IsAssignableFrom(objectType))
                {
                    return false;
                }
                return true;
            }

            /// <inheritdoc/>
            public override object? ReadJson(JsonReader reader, Type objectType,
                object? existingValue, JsonSerializer serializer)
            {
                switch (reader.Value)
                {
                    case null:
                        //
                        // Either really null or content is an array of bytes
                        // Read as int array to avoid infinite recursion.
                        //
                        var arr = serializer.Deserialize<int[]>(reader);
                        if (arr != null)
                        {
                            return Array.ConvertAll(arr, i => (byte)i);
                        }
                        return null;
                    case byte[] buffer:
                        return buffer;
                    case string base64:
                        return Convert.FromBase64String(base64);
                    default:
                        throw new FormatException("Current value is not buffer");
                }
            }

            /// <inheritdoc/>
            public override void WriteJson(JsonWriter writer,
                object? value, JsonSerializer serializer)
            {
                if (value is IReadOnlyCollection<byte> b)
                {
                    writer.WriteValue(b.ToArray());
                    return;
                }
                writer.WriteValue(value);
            }
        }
        /// <summary>
        /// Readonly set converter
        /// </summary>
        internal sealed class XmlElementConverter : JsonConverter<System.Xml.XmlElement>
        {
            /// <inheritdoc/>
            public override bool CanRead => true;

            /// <inheritdoc/>
            public override bool CanWrite => true;

            /// <inheritdoc/>
            public override System.Xml.XmlElement? ReadJson(JsonReader reader, Type objectType,
                System.Xml.XmlElement? existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                byte[] encoded;
                switch (reader.Value)
                {
                    case null:
                        var xmlSerializer = JsonSerializer.CreateDefault();
                        return xmlSerializer.Deserialize<System.Xml.XmlElement>(reader);
                    case byte[] buffer:
                        encoded = buffer;
                        break;
                    case string base64:
                        encoded = Convert.FromBase64String(base64);
                        break;
                    default:
                        throw new FormatException("Current value is not xml");
                }
                if (encoded == null || encoded.Length == 0)
                {
                    return null;
                }
                var xml = Encoding.UTF8.GetString(encoded);
                if (xml == null)
                {
                    return null;
                }
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);
                return doc.DocumentElement;
            }

            /// <inheritdoc/>
            public override void WriteJson(JsonWriter writer,
                System.Xml.XmlElement? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    var encoded = Encoding.UTF8.GetBytes(value.OuterXml);
                    writer.WriteValue(encoded);
                }
            }
        }

        /// <summary>
        /// Readonly set converter
        /// </summary>
        internal sealed class ReadOnlySetConverter : JsonConverter
        {
            /// <inheritdoc/>
            public override bool CanRead => true;

            /// <inheritdoc/>
            public override bool CanWrite => false;

            /// <inheritdoc/>
            public override bool CanConvert(Type objectType)
            {
                return objectType.GetCompatibleGenericInterface(
                    typeof(IReadOnlySet<>)) != null;
            }

            /// <inheritdoc/>
            public override object? ReadJson(JsonReader reader, Type objectType,
                object? existingValue, JsonSerializer serializer)
            {
                var elementType = objectType.GetGenericArguments()[0];
                return GetType().GetMethod(nameof(Read))!
                    .MakeGenericMethod(elementType)
                    .Invoke(null, new object[] { reader, serializer });
            }

            /// <summary>
            /// Read set
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="reader"></param>
            /// <param name="serializer"></param>
            public static IReadOnlySet<T?>? Read<T>(JsonReader reader,
                JsonSerializer serializer)
            {
                var set = serializer.Deserialize<T[]>(reader);
                return set != null ? new HashSet<T?>(set) : null;
            }

            /// <inheritdoc/>
            public override void WriteJson(JsonWriter writer, object? value,
                JsonSerializer serializer)
            {
                System.Diagnostics.Debug.Assert(true);
            }
        }

        /// <summary>
        /// Json veriant converter
        /// </summary>
        internal sealed class JsonVariantConverter : JsonConverter
        {
            /// <summary>
            /// Converter
            /// </summary>
            /// <param name="serializer"></param>
            public JsonVariantConverter(NewtonsoftJsonSerializer serializer)
            {
                _serializer = serializer;
            }

            /// <inheritdoc/>
            public override void WriteJson(JsonWriter writer, object? value,
                JsonSerializer serializer)
            {
                switch (value)
                {
                    case JsonVariantValue json:
                        json.Token.WriteTo(writer, serializer.Converters.ToArray());
                        break;
                    case VariantValue variant:
                        if (variant.IsNull())
                        {
                            writer.WriteNull();
                        }
                        else if (variant.IsListOfValues)
                        {
                            writer.WriteStartArray();
                            foreach (var item in variant.Values)
                            {
                                WriteJson(writer, item, serializer);
                            }
                            writer.WriteEndArray();
                        }
                        else if (variant.IsObject)
                        {
                            writer.WriteStartObject();
                            foreach (var key in variant.PropertyNames)
                            {
                                var item = variant[key];
                                if (item.IsNull())
                                {
                                    if (serializer.NullValueHandling != NullValueHandling.Include ||
                                        serializer.DefaultValueHandling != DefaultValueHandling.Include)
                                    {
                                        break;
                                    }
                                }
                                writer.WritePropertyName(key);
                                WriteJson(writer, item, serializer);
                            }
                            writer.WriteEndObject();
                        }
                        else if (variant.TryGetValue(out var primitive, CultureInfo.InvariantCulture))
                        {
                            serializer.Serialize(writer, primitive);
                        }
                        else
                        {
                            serializer.Serialize(writer, variant.Value);
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unexpected type passed");
                }
            }

            /// <inheritdoc/>
            public override object? ReadJson(JsonReader reader, Type objectType,
                object? existingValue, JsonSerializer serializer)
            {
                // Read variant from json
                var token = JToken.Load(reader);
                if (token.Type == JTokenType.Null)
                {
                    return null;
                }
                return new JsonVariantValue(token, _serializer);
            }

            /// <inheritdoc/>
            public override bool CanConvert(Type objectType)
            {
                return typeof(VariantValue).IsAssignableFrom(objectType);
            }

            private readonly NewtonsoftJsonSerializer _serializer;
        }

        /// <summary>
        /// Strategy to only camel case dictionary keys
        /// </summary>
        private class CamelCaseDictionaryKeys : CamelCaseNamingStrategy
        {
            /// <summary>
            /// Create strategy
            /// </summary>
            public CamelCaseDictionaryKeys()
            {
                ProcessDictionaryKeys = true;
            }

            /// <inheritdoc/>
            protected override string ResolvePropertyName(string name)
            {
                return name;
            }
        }
    }
}
