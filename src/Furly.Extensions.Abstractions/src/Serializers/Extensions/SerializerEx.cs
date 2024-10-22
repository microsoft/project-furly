// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Serializer extensions
    /// </summary>
    public static class SerializerEx
    {
        /// <summary>
        /// Serialize to byte array
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="type"></param>
        /// <param name="format"></param>
        public static ReadOnlyMemory<byte> SerializeObjectToMemory(
            this ISerializer serializer, object? o, Type? type = null,
            SerializeOption format = SerializeOption.None)
        {
            var writer = new ArrayBufferWriter<byte>();
            serializer.SerializeObject(writer, o, type, format);
            return writer.WrittenMemory;
        }

        /// <summary>
        /// Serialize to byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="format"></param>
        public static ReadOnlyMemory<byte> SerializeToMemory<T>(
            this ISerializer serializer, T? o,
            SerializeOption format = SerializeOption.None)
        {
            var writer = new ArrayBufferWriter<byte>();
            serializer.Serialize(writer, o, format);
            return writer.WrittenMemory;
        }
        /// <summary>
        /// Serialize to byte array
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="type"></param>
        /// <param name="format"></param>
        public static ReadOnlySequence<byte> SerializeToReadOnlySequence(
            this ISerializer serializer, object? o, Type? type = null,
            SerializeOption format = SerializeOption.None)
        {
            var writer = new ArrayBufferWriter<byte>();
            serializer.SerializeObject(writer, o, type, format);
            return new ReadOnlySequence<byte>(writer.WrittenMemory);
        }

        /// <summary>
        /// Serialize to byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="format"></param>
        public static ReadOnlySequence<byte> SerializeToReadOnlySequence<T>(
            this ISerializer serializer, T? o,
            SerializeOption format = SerializeOption.None)
        {
            var writer = new ArrayBufferWriter<byte>();
            serializer.Serialize(writer, o, format);
            return new ReadOnlySequence<byte>(writer.WrittenMemory);
        }

        /// <summary>
        /// Serialize to string
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="type"></param>
        /// <param name="format"></param>
        public static string SerializeObjectToString(
            this ISerializer serializer, object? o, Type? type = null,
            SerializeOption format = SerializeOption.None)
        {
            var memory = serializer.SerializeObjectToMemory(o, type, format);
            return serializer.ContentEncoding?.GetString(memory.Span)
                ?? Convert.ToBase64String(memory.Span);
        }

        /// <summary>
        /// Serialize to string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="o"></param>
        /// <param name="format"></param>
        public static string SerializeToString<T>(this ISerializer serializer,
            T? o, SerializeOption format = SerializeOption.None)
        {
            var memory = serializer.SerializeToMemory(o, format);
            return serializer.ContentEncoding?.GetString(memory.Span)
                ?? Convert.ToBase64String(memory.Span);
        }

        /// <summary>
        /// Deserialize from string
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="str"></param>
        /// <param name="type"></param>
        public static object? Deserialize(this ISerializer serializer,
            string str, Type type)
        {
            var buffer = serializer.ContentEncoding?.GetBytes(str)
                ?? Convert.FromBase64String(str);
            return serializer.Deserialize(buffer, type);
        }

        /// <summary>
        /// Deserialize from string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="str"></param>
        public static T? Deserialize<T>(this ISerializer serializer, string str)
        {
            return (T?)serializer.Deserialize(str, typeof(T));
        }

        /// <summary>
        /// Deserialize from buffer
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="buffer"></param>
        public static T? Deserialize<T>(this ISerializer serializer,
            ReadOnlyMemory<byte> buffer)
        {
            return (T?)serializer.Deserialize(buffer, typeof(T));
        }

        /// <summary>
        /// Deserialize from memory
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="buffer"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object? Deserialize(this ISerializer serializer,
            ReadOnlyMemory<byte> buffer, Type type)
        {
            return serializer.Deserialize(new ReadOnlySequence<byte>(buffer), type);
        }

        /// <summary>
        /// Deserialize from memory
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static VariantValue Parse(this ISerializer serializer,
            ReadOnlyMemory<byte> buffer)
        {
            return serializer.Parse(new ReadOnlySequence<byte>(buffer));
        }

        /// <summary>
        /// Deserialize from buffer
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="buffer"></param>
        public static T? Deserialize<T>(this ISerializer serializer,
            ReadOnlySequence<byte> buffer)
        {
            return (T?)serializer.Deserialize(buffer, typeof(T));
        }

        /// <summary>
        /// Convert to token.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="a"></param>
        public static VariantValue FromArray(this ISerializer serializer,
            params object?[] a)
        {
            return serializer.FromObject(a);
        }

        /// <summary>
        /// Parse string
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="str"></param>
        public static VariantValue Parse(this ISerializer serializer,
            string str)
        {
            var buffer = serializer.ContentEncoding?.GetBytes(str)
                ?? Convert.FromBase64String(str);
            return serializer.Parse(buffer);
        }
    }
}
