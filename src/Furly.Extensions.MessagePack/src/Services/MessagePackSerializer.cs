// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.MessagePack
{
    using Furly.Extensions.Serializers;
    using Furly.Exceptions;
    using global::MessagePack;
    using global::MessagePack.Formatters;
    using MsgPack = global::MessagePack.MessagePackSerializer;
    using global::MessagePack.Resolvers;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Message pack serializer
    /// </summary>
    public class MessagePackSerializer : IMessagePackSerializerOptionsProvider,
        IBinarySerializer
    {
        /// <inheritdoc/>
        public string MimeType => ContentMimeType.MsgPack;

        /// <inheritdoc/>
        public Encoding? ContentEncoding => null;

        /// <inheritdoc/>
        public MessagePackSerializerOptions Options { get; }

        /// <inheritdoc/>
        public IEnumerable<IFormatterResolver> Resolvers { get; }

        /// <summary>
        /// Create serializer
        /// </summary>
        /// <param name="providers"></param>
        public MessagePackSerializer(
            IEnumerable<IMessagePackFormatterResolverProvider>? providers = null)
        {
            // Create options
            var resolvers = new List<IFormatterResolver> {
                MessagePackVariantFormatterResolver.Instance,
                XmlElementFormatterResolver.Instance,
                ReadOnlySetResolver.Instance,
                AsyncEnumerableResolver.Instance,
            };
            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    var providedResolvers = provider.GetResolvers();
                    if (providedResolvers != null)
                    {
                        resolvers.AddRange(providedResolvers);
                    }
                }
            }
            resolvers.Add(StandardResolver.Instance);
            resolvers.Add(DynamicContractlessObjectResolver.Instance);
            Resolvers = resolvers;

            Options = MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithResolver(CompositeResolver.Create(Resolvers.ToArray()))
                ;
        }

        /// <inheritdoc/>
        public object? Deserialize(ReadOnlySequence<byte> buffer, Type type)
        {
            try
            {
                return MsgPack.Deserialize(type, buffer, Options);
            }
            catch (MessagePackSerializationException ex)
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
                return await MsgPack.DeserializeAsync(type, stream, Options,
                    ct).ConfigureAwait(false);
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<T?> DeserializeStreamAsync<T>(Stream stream,
            [EnumeratorCancellation] CancellationToken ct)
        {
            using (var reader = new MessagePackStreamReader(stream))
            {
                await foreach (var item in reader.ReadArrayAsync(ct).ConfigureAwait(false))
                {
                    T? result;
                    try
                    {
                        result = MsgPack.Deserialize<T>(item, Options, ct);
                    }
                    catch (MessagePackSerializationException ex)
                    {
                        throw new SerializerException(ex.Message, ex);
                    }
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<object?> DeserializeStreamAsync(Stream stream,
            Type type, [EnumeratorCancellation] CancellationToken ct)
        {
            using (var reader = new MessagePackStreamReader(stream))
            {
                await foreach (var item in reader.ReadArrayAsync(ct).ConfigureAwait(false))
                {
                    object? result;
                    try
                    {
                        result = MsgPack.Deserialize(type, item, Options, ct);
                    }
                    catch (MessagePackSerializationException ex)
                    {
                        throw new SerializerException(ex.Message, ex);
                    }
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async Task SerializeObjectAsync(Stream stream, object? o, Type? type,
            SerializeOption format, CancellationToken ct)
        {
            try
            {
                if (type != null)
                {
                    await MsgPack.SerializeAsync(type, stream, o, Options,
                        ct).ConfigureAwait(false);
                    return;
                }
                await MsgPack.SerializeAsync(stream, o, Options,
                    ct).ConfigureAwait(false);
            }
            catch (MessagePackSerializationException ex)
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
                if (type != null)
                {
                    MsgPack.Serialize(type, buffer, o, Options);
                }
                else
                {
                    MsgPack.Serialize(buffer, o, Options);
                }
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public VariantValue Parse(ReadOnlySequence<byte> buffer)
        {
            try
            {
#pragma warning disable CA2263 // Prefer generic overload when type is known
                var o = MsgPack.Deserialize(typeof(object), buffer, Options);
#pragma warning restore CA2263 // Prefer generic overload when type is known
                if (o is VariantValue v)
                {
                    return v;
                }
                return new MessagePackVariantValue(o, Options, false);
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public VariantValue FromObject(object? o)
        {
            try
            {
                return new MessagePackVariantValue(o, Options, true);
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializerException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Value wrapper
        /// </summary>
        internal class MessagePackVariantValue : VariantValue
        {
            /// <summary>
            /// Create value
            /// </summary>
            /// <param name="value"></param>
            /// <param name="serializer"></param>
            /// <param name="typed">Whether the object is the
            /// original type or the generated one</param>
            /// <param name="parentUpdate"></param>
            internal MessagePackVariantValue(object? value,
                MessagePackSerializerOptions serializer, bool typed,
                Action<object?>? parentUpdate = null)
            {
                _options = serializer;
                _update = parentUpdate;
                _value = typed ? ToTypeLess(value) : value;
            }

            /// <inheritdoc/>
            protected override VariantValueType GetValueType()
            {
                if (_value == null)
                {
                    return VariantValueType.Null;
                }
                var rawValue = GetRawValue();
                if (rawValue == null)
                {
                    return VariantValueType.Null;
                }
                var type = rawValue.GetType();
                if (typeof(byte[]) == type ||
                    typeof(string) == type)
                {
                    return VariantValueType.Primitive;
                }
                if (type.IsArray ||
                    typeof(IList<object>).IsAssignableFrom(type) ||
                    typeof(IEnumerable<object>).IsAssignableFrom(type))
                {
                    return VariantValueType.Values;
                }
                if (typeof(IDictionary<object, object>).IsAssignableFrom(type))
                {
                    return VariantValueType.Complex;
                }
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = type.GetGenericArguments()[0];
                }
                if (typeof(bool) == type ||
                    typeof(Guid) == type ||
                    typeof(DateTime) == type ||
                    typeof(DateTimeOffset) == type ||
                    typeof(TimeSpan) == type ||
                    typeof(uint) == type ||
                    typeof(int) == type ||
                    typeof(ulong) == type ||
                    typeof(long) == type ||
                    typeof(sbyte) == type ||
                    typeof(byte) == type ||
                    typeof(ushort) == type ||
                    typeof(short) == type ||
                    typeof(char) == type ||
                    typeof(float) == type ||
                    typeof(double) == type ||
                    typeof(decimal) == type ||
                    typeof(BigInteger) == type)
                {
                    return VariantValueType.Primitive;
                }
                if (type.GetProperties().Length > 0)
                {
                    return VariantValueType.Complex;
                }
                // TODO: Throw?
                return VariantValueType.Primitive;
            }

            /// <inheritdoc/>
            protected override object? GetRawValue()
            {
                if (_value is Uri u)
                {
                    return u.ToString();
                }
                if (_value is object[] o && o.Length == 2 && o[0] is DateTime dt)
                {
                    // Datetime offset encoding convention
                    switch (o[1])
                    {
                        case uint:
                        case int:
                        case ulong:
                        case long:
                        case ushort:
                        case short:
                        case byte:
                        case sbyte:
                            var offset = Convert.ToInt64(o[1], CultureInfo.InvariantCulture);
                            if (offset == 0)
                            {
                                return dt;
                            }
                            return new DateTimeOffset(dt, TimeSpan.FromTicks(offset));
                    }
                }
                return _value;
            }

            /// <inheritdoc/>
            protected override IEnumerable<string> GetObjectProperties()
            {
                if (_value is IDictionary<object, object> o)
                {
                    return o.Keys.Select(p => p.ToString() ?? string.Empty);
                }
                return [];
            }

            /// <inheritdoc/>
            protected override IEnumerable<VariantValue> GetArrayElements()
            {
                if (_value is IList<object> array)
                {
                    return array.Select(i =>
                        new MessagePackVariantValue(i, _options, false));
                }
                return [];
            }

            /// <inheritdoc/>
            protected override int GetArrayCount()
            {
                if (_value is IList<object> array)
                {
                    return array.Count;
                }
                return 0;
            }

            /// <inheritdoc/>
            public override void AssignValue(object? value)
            {
                if (_update == null)
                {
                    throw new NotSupportedException("Not an object or array");
                }
                _update(value);
                _value = value;
            }

            /// <inheritdoc/>
            public override VariantValue Copy(bool shallow)
            {
                if (_value == null)
                {
                    return new MessagePackVariantValue(null, _options, false);
                }
                try
                {
                    return new MessagePackVariantValue(_value, _options, true);
                }
                catch (MessagePackSerializationException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            /// <inheritdoc/>
            public override object? ConvertTo(Type type)
            {
                if (_value == null)
                {
                    return null;
                }
                var valueType = _value.GetType();
                if (type.IsAssignableFrom(valueType))
                {
                    return _value;
                }
                try
                {
                    var mem = new ArrayBufferWriter<byte>();
                    MsgPack.Serialize(mem, _value, _options);
                    var buffer = mem.WrittenMemory;
                    // Special case - convert byte array to buffer if not bin to begin.
                    if (type == typeof(byte[]) && valueType.IsArray)
                    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
                        return ((IList<byte>?)MsgPack.Deserialize(typeof(IList<byte>),
                            buffer, _options))?.ToArray();
#pragma warning restore CA2263 // Prefer generic overload when type is known
                    }
                    return MsgPack.Deserialize(type, buffer, _options);
                }
                catch (MessagePackSerializationException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            /// <inheritdoc/>
            public override bool TryGetProperty(string key,
                [NotNullWhen(true)] out VariantValue? value)
            {
                if (_value is IDictionary<object, object?> o)
                {
                    var success = o.FirstOrDefault(kv => key.Equals((string)kv.Key,
                        StringComparison.OrdinalIgnoreCase));
                    if (success.Value != null)
                    {
                        value = new MessagePackVariantValue(success.Value, _options, false,
                            v => o[success.Key] = v);
                        return true;
                    }
                }
                value = null;
                return false;
            }

            /// <inheritdoc/>
            public override bool TryGetElement(int index,
                [NotNullWhen(true)] out VariantValue? value)
            {
                if (index >= 0 && _value is IList<object?> o && index < o.Count)
                {
                    value = new MessagePackVariantValue(o[index], _options, false,
                        v => o[index] = v);
                    return true;
                }
                value = null;
                return false;
            }

            /// <inheritdoc/>
            protected override VariantValue AddProperty(string propertyName)
            {
                if (_value is IDictionary<object, object?> o)
                {
                    return new MessagePackVariantValue(null, _options, false,
                        v => o[propertyName] = v);
                }
                throw new NotSupportedException("Not an object");
            }

            /// <inheritdoc/>
            protected override bool TryEqualsVariant(VariantValue? v, out bool equality)
            {
                if (v is MessagePackVariantValue packed)
                {
                    equality = DeepEquals(_value, packed._value);
                    return true;
                }

                // Special comparison to timespan
                if ((v?.IsTimeSpan ?? false) && !IsArray)
                {
                    if (IsInteger || IsDecimal)
                    {
                        var ticks = Convert.ToInt64(_value, CultureInfo.InvariantCulture);
                        equality = v.Equals((VariantValue)TimeSpan.FromTicks(ticks));
                        return true;
                    }
                }
                return base.TryEqualsVariant(v, out equality);
            }

            /// <summary>
            /// Compare tokens in more consistent fashion
            /// </summary>
            /// <param name="t1"></param>
            /// <param name="t2"></param>
            internal static bool DeepEquals(object? t1, object? t2)
            {
                if (t1 == null || t2 == null)
                {
                    return t1 == t2;
                }

                // Test object equals
                if (t1 is IDictionary<object, object> o1 &&
                    t2 is IDictionary<object, object> o2)
                {
                    // Compare properties in order of key
                    var props1 = o1.OrderBy(k => k.Key).Select(k => k.Value);
                    var props2 = o2.OrderBy(k => k.Key).Select(k => k.Value);
                    return props1.SequenceEqual(props2,
                        Compare.Using<object>(DeepEquals));
                }

                // Test array
                if (t1 is object[] c1 && t2 is object[] c2)
                {
                    return c1.SequenceEqual(c2,
                        Compare.Using<object>(DeepEquals));
                }

                // Test array
                if (t1 is byte[] b1 && t2 is byte[] b2)
                {
                    return b1.SequenceEqual(b2);
                }

                // Test value equals
                if (t1.Equals(t2))
                {
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Convert to typeless object
            /// </summary>
            /// <param name="value"></param>
            /// <exception cref="SerializerException"></exception>
            internal object? ToTypeLess(object? value)
            {
                if (value == null)
                {
                    return null;
                }
                try
                {
                    var mem = new ArrayBufferWriter<byte>();
                    MsgPack.Serialize(mem, value, _options);
                    var buffer = mem.WrittenMemory;
#pragma warning disable CA2263 // Prefer generic overload when type is known
                    return MsgPack.Deserialize(typeof(object), buffer, _options);
#pragma warning restore CA2263 // Prefer generic overload when type is known
                }
                catch (MessagePackSerializationException ex)
                {
                    throw new SerializerException(ex.Message, ex);
                }
            }

            private readonly MessagePackSerializerOptions _options;
            private readonly Action<object?>? _update;
            internal object? _value;
        }

        /// <summary>
        /// Message pack resolver
        /// </summary>
        internal class MessagePackVariantFormatterResolver : IFormatterResolver
        {
            public static readonly MessagePackVariantFormatterResolver Instance =
                new();

            /// <inheritdoc/>
            public MessagePackVariantFormatterResolver()
            {
                _cache = new ConcurrentDictionary<Type, IMessagePackFormatter>();
            }

            /// <inheritdoc/>
            public IMessagePackFormatter<T>? GetFormatter<T>()
            {
                if (typeof(VariantValue).IsAssignableFrom(typeof(T)))
                {
                    return (IMessagePackFormatter<T>)GetVariantFormatter(typeof(T));
                }
                return null;
            }

            /// <summary>
            /// Create Message pack variant formater of specifed type
            /// </summary>
            /// <param name="type"></param>
            internal IMessagePackFormatter GetVariantFormatter(Type type)
            {
                return _cache.GetOrAdd(type, t =>
                {
                    var formatter = Activator.CreateInstance(
                        typeof(MessagePackVariantFormatter<>).MakeGenericType(t));
                    if (formatter == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create variant formatter for {type.Name}.");
                    }
                    return (IMessagePackFormatter)formatter;
                });
            }

            /// <summary>
            /// Variant formatter
            /// </summary>
            /// <typeparam name="T"></typeparam>
            internal sealed class MessagePackVariantFormatter<T> : IMessagePackFormatter<T?>
                where T : VariantValue
            {
                /// <inheritdoc/>
                public void Serialize(ref MessagePackWriter writer, T? value,
                    MessagePackSerializerOptions options)
                {
                    if (value is MessagePackVariantValue packed)
                    {
                        MsgPack.Serialize(ref writer, packed._value, options);
                    }
                    else if (value is null)
                    {
                        writer.WriteNil();
                    }
                    else
                    {
                        if (value.IsNull())
                        {
                            writer.WriteNil();
                        }
                        else if (value.IsListOfValues)
                        {
                            writer.WriteArrayHeader(value.Count);
                            foreach (var item in value.Values)
                            {
                                MsgPack.Serialize(ref writer, item, options);
                            }
                        }
                        else if (value.IsObject)
                        {
                            // Serialize objects as key value pairs
                            var dict = value.PropertyNames
                                .ToDictionary(k => k, k => value[k]);
                            MsgPack.Serialize(ref writer, dict, options);
                        }
                        else if (value.TryGetValue(out var primitive, CultureInfo.InvariantCulture))
                        {
                            MsgPack.Serialize(ref writer, primitive, options);
                        }
                        else
                        {
                            MsgPack.Serialize(ref writer, value.Value, options);
                        }
                    }
                }

                /// <inheritdoc/>
                public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    // Read variant from reader
                    var o = MsgPack.Deserialize<object>(ref reader, options);
                    if (o == null)
                    {
                        return null;
                    }
                    return new MessagePackVariantValue(o, options, false) as T;
                }
            }

            private readonly ConcurrentDictionary<Type, IMessagePackFormatter> _cache;
        }

        /// <summary>
        /// XmlElement resolver
        /// </summary>
        internal class XmlElementFormatterResolver : IFormatterResolver
        {
            public static readonly XmlElementFormatterResolver Instance = new();

            /// <inheritdoc/>
            public IMessagePackFormatter<T>? GetFormatter<T>()
            {
                if (typeof(System.Xml.XmlElement) == typeof(T))
                {
                    return (IMessagePackFormatter<T>)
                        (IMessagePackFormatter)new XmlElementFormatter();
                }
                return null;
            }

            /// <summary>
            /// xml formatter
            /// </summary>
            internal sealed class XmlElementFormatter : IMessagePackFormatter<System.Xml.XmlElement?>
            {
                /// <inheritdoc/>
                public void Serialize(ref MessagePackWriter writer, System.Xml.XmlElement? value,
                    MessagePackSerializerOptions options)
                {
                    if (value == null)
                    {
                        writer.WriteNil();
                    }
                    else
                    {
                        var encoded = Encoding.UTF8.GetBytes(value.OuterXml);
                        writer.Write(encoded);
                    }
                }

                /// <inheritdoc/>
                public System.Xml.XmlElement? Deserialize(ref MessagePackReader reader,
                    MessagePackSerializerOptions options)
                {
                    if (reader.IsNil)
                    {
                        return null;
                    }

                    byte[] encoded;
                    if (reader.NextMessagePackType == MessagePackType.String)
                    {
                        //
                        // Possible that it ended up as base 64 string when
                        // transcoding from json.
                        //
                        encoded = Convert.FromBase64String(reader.ReadString() ?? string.Empty);
                    }
                    else if (reader.NextMessagePackType == MessagePackType.Binary)
                    {
                        var binary = reader.ReadBytes();
                        if (!binary.HasValue)
                        {
                            return null;
                        }
                        encoded = binary.Value.ToArray();
                    }
                    else
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
            }
        }

        /// <summary>
        /// ReadOnly Set resolver
        /// </summary>
        private class ReadOnlySetResolver : IFormatterResolver
        {
            public static readonly ReadOnlySetResolver Instance = new();

            /// <inheritdoc/>
            public ReadOnlySetResolver()
            {
                _cache = new ConcurrentDictionary<Type, IMessagePackFormatter>();
            }

            /// <inheritdoc/>
            public IMessagePackFormatter<T>? GetFormatter<T>()
            {
                var type = typeof(T).GetCompatibleGenericInterface(typeof(IReadOnlySet<>));
                if (type != null)
                {
                    return (IMessagePackFormatter<T>)GetReadOnlySetFormatter(
                        typeof(T), type.GetGenericArguments()[0]);
                }
                return null;
            }

            /// <summary>
            /// Create Message pack variant formater of specifed type
            /// </summary>
            /// <param name="type"></param>
            /// <param name="elementType"></param>
            internal IMessagePackFormatter GetReadOnlySetFormatter(Type type, Type elementType)
            {
                return _cache.GetOrAdd(type, t =>
                {
                    var formatter = Activator.CreateInstance(typeof(ReadOnlySetFormatter<,>)
                        .MakeGenericType(t, elementType));
                    if (formatter == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create set formatter for {type.Name}.");
                    }
                    return (IMessagePackFormatter)formatter;
                });
            }

            /// <summary>
            /// Set formatter
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <typeparam name="TElement"></typeparam>
            internal sealed class ReadOnlySetFormatter<T, TElement> : IMessagePackFormatter<T?>
            {
                /// <inheritdoc/>
                public void Serialize(ref MessagePackWriter writer, T? value,
                    MessagePackSerializerOptions options)
                {
                    MsgPack.Serialize(ref writer, (IEnumerable<TElement?>?)value, options);
                }

                /// <inheritdoc/>
                public T? Deserialize(ref MessagePackReader reader,
                    MessagePackSerializerOptions options)
                {
                    var set = MsgPack.Deserialize<TElement[]>(ref reader, options);
                    if (set != null)
                    {
                        return (T?)(IReadOnlySet<TElement?>?)new HashSet<TElement?>(set);
                    }
                    return default;
                }
            }

            private readonly ConcurrentDictionary<Type, IMessagePackFormatter> _cache;
        }

        /// <summary>
        /// ReadOnly Set resolver
        /// </summary>
        internal class AsyncEnumerableResolver : IFormatterResolver
        {
            public static readonly AsyncEnumerableResolver Instance = new();

            /// <inheritdoc/>
            public AsyncEnumerableResolver()
            {
                _cache = new ConcurrentDictionary<Type, IMessagePackFormatter>();
            }

            /// <inheritdoc/>
            public IMessagePackFormatter<T>? GetFormatter<T>()
            {
                var type = typeof(T).GetCompatibleGenericInterface(typeof(IAsyncEnumerable<>));
                if (type != null)
                {
                    return (IMessagePackFormatter<T>)GetAsyncEnumerableFormatter(
                        typeof(T), type.GetGenericArguments()[0]);
                }
                return null;
            }

            /// <summary>
            /// Create Message pack variant formater of specifed type
            /// </summary>
            /// <param name="type"></param>
            /// <param name="elementType"></param>
            internal IMessagePackFormatter GetAsyncEnumerableFormatter(Type type, Type elementType)
            {
                return _cache.GetOrAdd(type, t =>
                {
                    var formatter = Activator.CreateInstance(
                        typeof(AsyncEnumerableFormatter<,>).MakeGenericType(t, elementType));
                    if (formatter == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create async enumerable formatter for {type.Name}.");
                    }
                    return (IMessagePackFormatter)formatter;
                });
            }

            /// <summary>
            /// Set formatter
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <typeparam name="E"></typeparam>
            internal sealed class AsyncEnumerableFormatter<T, E> : IMessagePackFormatter<T?>
            {
                /// <inheritdoc/>
                public void Serialize(ref MessagePackWriter writer, T? value,
                    MessagePackSerializerOptions options)
                {
                    var enumerable = (IAsyncEnumerable<E?>?)value;
                    MsgPack.Serialize(ref writer, enumerable?.ToEnumerable().ToList(), options);
                }

                /// <inheritdoc/>
                public T? Deserialize(ref MessagePackReader reader,
                    MessagePackSerializerOptions options)
                {
                    return (T?)MsgPack.Deserialize<List<E>?>(ref reader, options)?.ToAsyncEnumerable();
                }
            }

            private readonly ConcurrentDictionary<Type, IMessagePackFormatter> _cache;
        }
    }
}
