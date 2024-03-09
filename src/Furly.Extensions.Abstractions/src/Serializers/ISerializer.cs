// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Pluggable serializer
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Mime type the serializer can emit or decode.
        /// </summary>
        string MimeType { get; }

        /// <summary>
        /// Encoding used
        /// </summary>
        Encoding? ContentEncoding { get; }

        /// <summary>
        /// Serialize to memory
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="o"></param>
        /// <param name="type"></param>
        /// <param name="format"></param>
        void SerializeObject(IBufferWriter<byte> buffer,
            object? o, Type? type = null,
            SerializeOption format = SerializeOption.None);

        /// <summary>
        /// Serialize to memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <param name="o"></param>
        /// <param name="format"></param>
        void Serialize<T>(IBufferWriter<byte> buffer, T? o,
            SerializeOption format = SerializeOption.None)
        {
            SerializeObject(buffer, o, typeof(T), format);
        }

        /// <summary>
        /// Serialize to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="o"></param>
        /// <param name="type"></param>
        /// <param name="format"></param>
        /// <param name="ct"></param>
        Task SerializeObjectAsync(Stream stream,
            object? o, Type? type = null,
            SerializeOption format = SerializeOption.None,
            CancellationToken ct = default);

        /// <summary>
        /// Serialize to stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="o"></param>
        /// <param name="format"></param>
        /// <param name="ct"></param>
        Task SerializeAsync<T>(Stream stream, T? o,
            SerializeOption format = SerializeOption.None,
            CancellationToken ct = default)
        {
            return SerializeObjectAsync(stream, o, typeof(T), format, ct);
        }

        /// <summary>
        /// Deserialize from memory
        /// </summary>
        /// <param name="type"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        object? Deserialize(ReadOnlySequence<byte> buffer, Type type);

        /// <summary>
        /// Deserialize from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        /// <param name="sizeHint"></param>
        /// <param name="ct"></param>
        ValueTask<object?> DeserializeAsync(Stream stream, Type type,
            int? sizeHint = null, CancellationToken ct = default);

        /// <summary>
        /// Deserialize of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="sizeHint"></param>
        /// <param name="ct"></param>
        async ValueTask<T?> DeserializeAsync<T>(Stream stream,
            int? sizeHint = null, CancellationToken ct = default)
        {
            return (T?)await DeserializeAsync(stream, typeof(T),
                sizeHint, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Deserialize from stream in a stream of T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="ct"></param>
        IAsyncEnumerable<T?> DeserializeStreamAsync<T>(
            Stream stream, CancellationToken ct = default);

        /// <summary>
        /// Deserialize from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        /// <param name="ct"></param>
        IAsyncEnumerable<object?> DeserializeStreamAsync(
            Stream stream, Type type, CancellationToken ct = default);

        /// <summary>
        /// Deserialize to variant value
        /// </summary>
        /// <param name="buffer"></param>
        VariantValue Parse(ReadOnlySequence<byte> buffer);

        /// <summary>
        /// Convert to token.
        /// </summary>
        /// <param name="o"></param>
        VariantValue FromObject(object? o);
    }
}
