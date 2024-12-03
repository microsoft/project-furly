// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Protocol
{
    using Furly.Tunnel.Models;
    using Furly.Extensions.Serializers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Serializes and deserialize models
    /// </summary>
    internal static class HttpTunnelSerializer
    {
        /// <summary>
        /// Serialize request into buffer chunks
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="tunnelRequest"></param>
        /// <param name="maxBufferSize"></param>
        /// <returns></returns>
        public static IReadOnlyList<byte[]> SerializeRequest(this ISerializer serializer,
            HttpTunnelRequestModel tunnelRequest, int maxBufferSize)
        {
            // Serialize data
            var buffers = new List<byte[]>();
            using (var header = new MemoryStream())
            using (var writer = new BinaryWriter(header))
            {
                var payload = tunnelRequest.Body;

                // Serialize header (0)
                tunnelRequest.Body = null;
                var headerBuffer =
                    serializer.SerializeToMemory(tunnelRequest).ToArray().Zip();

                writer.Write(headerBuffer.Length);
                writer.Write(headerBuffer);

                // Assume chunk size and payload size also written
                var remainingRoom = maxBufferSize - (int)(header.Position + 8);
                if (remainingRoom < 0)
                {
                    throw new ArgumentException("Header too large to sent");
                }

                // Create chunks from payload
                if (payload?.Length > 0)
                {
                    // Fill remaining room with payload
                    remainingRoom = Math.Min(remainingRoom, payload.Length);
                    writer.Write(remainingRoom);
                    writer.Write(payload, 0, remainingRoom);

                    // Create remaining chunks
                    for (; remainingRoom < payload.Length; remainingRoom += maxBufferSize)
                    {
                        var length = Math.Min(payload.Length - remainingRoom, maxBufferSize);
                        var chunk = payload.AsSpan(remainingRoom, length).ToArray();
                        buffers.Add(chunk);
                    }
                    writer.Write(buffers.Count);
                }
                else
                {
                    writer.Write(0);
                    writer.Write(0);
                }
                // Insert header as first buffer
                buffers.Insert(0, header.ToArray());
            }
            return buffers;
        }

        /// <summary>
        /// Deserialize request number 0
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="payload"></param>
        /// <param name="request"></param>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static byte[] DeserializeRequest0(this ISerializer serializer,
            byte[] payload, out HttpTunnelRequestModel request, out int chunks)
        {
            // Deserialize data
            using (var header = new MemoryStream(payload))
            using (var reader = new BinaryReader(header))
            {
                var headerLen = reader.ReadInt32();
                if (headerLen > payload.Length - 8)
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var headerBuf = reader.ReadBytes(headerLen);
                var bufferLen = reader.ReadInt32();
                if (bufferLen > payload.Length - (headerLen + 8))
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var chunk0 = bufferLen > 0 ? reader.ReadBytes(bufferLen) : null;
                chunks = reader.ReadInt32();
                if (chunks > kMaxNumberOfChunks)
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var result = serializer.Deserialize<HttpTunnelRequestModel>(
                    headerBuf.Unzip());
                if (result == null)
                {
                    throw new ArgumentException("Bad request.");
                }
                request = result;
                return chunk0 ?? [];
            }
        }

        /// <summary>
        /// Serialize request into buffer chunks
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="tunnelResponse"></param>
        /// <param name="maxBufferSize"></param>
        /// <returns></returns>
        public static IReadOnlyList<byte[]> SerializeResponse(this ISerializer serializer,
            HttpTunnelResponseModel tunnelResponse, int maxBufferSize)
        {
            // Serialize data
            var buffers = new List<byte[]>();
            using (var header = new MemoryStream())
            using (var writer = new BinaryWriter(header))
            {
                var payload = tunnelResponse.Payload;

                // Serialize header (0)
                tunnelResponse.Payload = null;
                var headerBuffer =
                    serializer.SerializeToMemory(tunnelResponse).ToArray().Zip();

                writer.Write(headerBuffer.Length);
                writer.Write(headerBuffer);

                // Assume chunk size and payload size also written
                var remainingRoom = maxBufferSize - (int)(header.Position + 8);
                if (remainingRoom < 0)
                {
                    throw new ArgumentException("Header too large to sent");
                }

                // Create chunks from payload
                if (payload?.Length > 0)
                {
                    // Fill remaining room with payload
                    remainingRoom = Math.Min(remainingRoom, payload.Length);
                    writer.Write(remainingRoom);
                    writer.Write(payload, 0, remainingRoom);

                    // Create remaining chunks
                    for (; remainingRoom < payload.Length; remainingRoom += maxBufferSize)
                    {
                        var length = Math.Min(payload.Length - remainingRoom, maxBufferSize);
                        var chunk = payload.AsSpan(remainingRoom, length).ToArray();
                        buffers.Add(chunk);
                    }
                    writer.Write(buffers.Count);
                }
                else
                {
                    writer.Write(0);
                    writer.Write(0);
                }
                // Insert header as first buffer
                buffers.Insert(0, header.ToArray());
            }
            return buffers;
        }

        /// <summary>
        /// Deserialize response number 0
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="payload"></param>
        /// <param name="response"></param>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static byte[] DeserializeResponse0(this ISerializer serializer,
            byte[] payload, out HttpTunnelResponseModel response, out int chunks)
        {
            // Deserialize data
            using (var header = new MemoryStream(payload))
            using (var reader = new BinaryReader(header))
            {
                var headerLen = reader.ReadInt32();
                if (headerLen > payload.Length - 8)
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var headerBuf = reader.ReadBytes(headerLen);
                var bufferLen = reader.ReadInt32();
                if (bufferLen > payload.Length - (headerLen + 8))
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var chunk0 = bufferLen > 0 ? reader.ReadBytes(bufferLen) : null;
                chunks = reader.ReadInt32();
                if (chunks > kMaxNumberOfChunks)
                {
                    throw new ArgumentException("Bad encoding length");
                }
                var result = serializer.Deserialize<HttpTunnelResponseModel>(
                    headerBuf.Unzip());
                if (result == null)
                {
                    throw new ArgumentException("Bad request.");
                }
                response = result;
                return chunk0 ?? [];
            }
        }

        /// <summary>
        /// Unpack payloads
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="buffers"></param>
        /// <returns></returns>
        public static byte[] Unpack(this byte[] buffer, IReadOnlyList<byte[]> buffers)
        {
            return buffer.YieldReturn().Concat(buffers.Skip(1)).ToArray().Unpack();
        }

        /// <summary>
        /// Unpack payloads
        /// </summary>
        /// <param name="buffers"></param>
        /// <returns></returns>
        public static byte[] Unpack(this byte[][] buffers)
        {
            byte[] payload;
            if (buffers.Length > 1)
            {
                // Combine chunks
                using (var stream = new MemoryStream())
                {
                    foreach (var chunk in buffers)
                    {
                        stream.Write(chunk);
                    }
                    payload = stream.ToArray();
                }
            }
            else if (buffers.Length == 1 && buffers[0].Length > 0)
            {
                payload = buffers[0];
            }
            else
            {
                payload = [];
            }
            return payload;
        }

        private const int kMaxNumberOfChunks = 1024;
    }
}
