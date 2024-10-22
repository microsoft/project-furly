// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Protocol
{
    using Furly.Tunnel;
    using Furly.Tunnel.Models;
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Chunked method provide reliable any size send/receive.
    /// The method invoker allows the recreation of the original
    /// messages through its $call endpoint, i.e., it is not
    /// just server but also a method invoker.
    /// </summary>
    internal sealed class ChunkMethodInvoker : IMethodInvoker, IDisposable
    {
        /// <inheritdoc/>
        public string MethodName => MethodNames.Call;

        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="timeout"></param>
        /// <param name="timeProvider"></param>
        public ChunkMethodInvoker(IJsonSerializer serializer, ILogger logger,
            TimeSpan timeout, TimeProvider timeProvider)
        {
            _serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
            _timeout = timeout;
            _timeProvider = timeProvider;
            _requests = new ConcurrentDictionary<string, ChunkProcessor>();
            _timer = new Timer(_ => OnTimer(), null,
                kTimeoutCheckInterval, kTimeoutCheckInterval);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _timer.Dispose();
            _requests.Clear();
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> InvokeAsync(
            ReadOnlyMemory<byte> payload, string contentType, IRpcHandler context,
            CancellationToken ct)
        {
            var request = _serializer.Deserialize<MethodChunkModel>(payload)
                ?? throw new ArgumentException("Payload invalid", nameof(payload));
            ChunkProcessor? processor;
            if (request.Handle != null)
            {
                if (!_requests.TryGetValue(request.Handle, out processor))
                {
                    throw new MethodCallStatusException((int)HttpStatusCode.RequestTimeout,
                        $"No handle {request.Handle}");
                }
            }
            else
            {
                var handle = Interlocked.Increment(ref _requestCounter)
                    .ToString(CultureInfo.InvariantCulture);
                processor = new ChunkProcessor(this, handle, request.MethodName,
                    request.ContentType, request.ContentLength, request.MaxChunkLength,
                    request.Timeout ?? _timeout, request.Properties);

                if (!_requests.TryAdd(handle, processor))
                {
                    throw new MethodCallStatusException((int)HttpStatusCode.InternalServerError,
                        $"Adding handle {handle} failed.");
                }
            }
            var response = await processor.ProcessAsync(context, request,
                ct).ConfigureAwait(false);
            return _serializer.SerializeToMemory(response).ToArray();
        }

        /// <summary>
        /// Manage requests
        /// </summary>
        private void OnTimer()
        {
            foreach (var item in _requests.Values)
            {
                if (item.IsTimedOut)
                {
                    _logger.LogDebug("Timed out on handle {Handle}.", item.Handle);
                    _requests.TryRemove(item.Handle, out _);
                }
            }
        }

        /// <summary>
        /// Processes chunks
        /// </summary>
        private sealed class ChunkProcessor
        {
            /// <summary>
            /// Request handle
            /// </summary>
            public string Handle { get; }

            /// <summary>
            /// Whether the request timed out
            /// </summary>
            public bool IsTimedOut
                => _outer._timeProvider.GetElapsedTime(_lastActivity) > _timeout;

            /// <summary>
            /// Create chunk
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="handle"></param>
            /// <param name="method"></param>
            /// <param name="contentType"></param>
            /// <param name="contentLength"></param>
            /// <param name="maxChunkLength"></param>
            /// <param name="timeout"></param>
            /// <param name="properties"></param>
            public ChunkProcessor(ChunkMethodInvoker outer, string? handle,
                string? method, string? contentType, int? contentLength,
                int? maxChunkLength, TimeSpan timeout,
                IDictionary<string, string>? properties)
            {
                Handle = handle ??
                    throw new ArgumentNullException(nameof(handle));
                _outer = outer ??
                    throw new ArgumentNullException(nameof(outer));
                _method = method ??
                    throw new ArgumentNullException(nameof(method));
                if (contentLength == null)
                {
                    throw new ArgumentNullException(nameof(contentLength));
                }
                _payload = new byte[contentLength.Value];
                _timeout = timeout;
                _properties = properties;
                _lastActivity = _outer._timeProvider.GetTimestamp();
                _maxChunkLength = maxChunkLength ?? 64 * 1024;
                _contentType = contentType ?? ContentMimeType.Json;
            }

            /// <summary>
            /// Process request and return response
            /// </summary>
            /// <param name="handler"></param>
            /// <param name="request"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public async Task<MethodChunkModel> ProcessAsync(IRpcHandler handler,
                MethodChunkModel request, CancellationToken ct)
            {
                var status = 200;
                var payload = request.Payload ?? Array.Empty<byte>();
                if (_sent == -1)
                {
                    // Receiving
                    Buffer.BlockCopy(payload, 0, _payload, _received,
                        payload.Length);
                    _received += payload.Length;
                    if (_received < _payload.Length)
                    {
                        // Continue upload
                        _lastActivity = _outer._timeProvider.GetTimestamp();
                        _outer._logger.LogDebug("Received on handle {Handle}", Handle);
                        return new MethodChunkModel
                        {
                            Handle = Handle
                        };
                    }

                    DistributedContextPropagator.Current.ExtractTraceIdAndState(_properties,
                    static (object? carrier, string fieldName, out string? fieldValue,
                        out IEnumerable<string>? fieldValues) =>
                    {
                        fieldValues = default;
                        fieldValue = carrier is IDictionary<string, string> p ?
                            p[fieldName] : default;
                    }, out var requestId, out var traceState);

                    using var activity = CreateActivity(_method, requestId, traceState);
                    try
                    {
                        // Process
                        var result = await handler.InvokeAsync(_method,
                            new ReadOnlySequence<byte>(_payload.Unzip()),
                            _contentType, ct).ConfigureAwait(false);
                        // Set response payload
                        _payload = result.ToArray().Zip();
                    }
                    catch (MethodCallStatusException mex)
                    {
                        _payload = mex.Serialize(_outer._serializer).ToArray().Zip();
                        status = mex.Details.Status ?? 500;
                    }
                    catch (Exception ex)
                    {
                        // Unexpected
                        status = (int)HttpStatusCode.InternalServerError;
                        _outer._logger.LogError(ex,
                            "Processing message resulted in unexpected error");
                    }
                    _sent = 0;
                }

                // Sending
                var length = Math.Min(_payload.Length - _sent, _maxChunkLength);
                var buffer = new byte[length];
                Buffer.BlockCopy(_payload, _sent, buffer, 0, buffer.Length);
                var response = new MethodChunkModel
                {
                    ContentLength = _sent == 0 ? _payload.Length : null,
                    Status = _sent == 0 && status != 200 ? status : null,
                    Payload = buffer
                };
                _sent += length;
                if (_sent == _payload.Length)
                {
                    // Done - remove ourselves
                    _outer._requests.TryRemove(Handle, out _);
                    _outer._logger.LogDebug("Completed handle {Handle}", Handle);
                }
                else
                {
                    response.Handle = Handle;
                    _lastActivity = _outer._timeProvider.GetTimestamp();
                    _outer._logger.LogDebug("Responded on handle {Handle}", Handle);
                }
                return response;

                static Activity? CreateActivity(string name, string? requestId, string? traceState)
                {
                    if (!kActivity.HasListeners())
                    {
                        return null;
                    }
                    if (ActivityContext.TryParse(requestId, traceState, true, out var context))
                    {
                        return kActivity.CreateActivity(name, ActivityKind.Server, context);
                    }
                    else
                    {
                        // Pass in the ID we got from the headers if there was one.
                        return kActivity.CreateActivity(name, ActivityKind.Server,
                            string.IsNullOrEmpty(requestId) ? null! : requestId);
                    }
                }
            }

            private readonly IDictionary<string, string>? _properties;
            private readonly ChunkMethodInvoker _outer;
            private readonly string _method;
            private readonly string _contentType;
            private readonly TimeSpan _timeout;
            private readonly int _maxChunkLength;
            private byte[] _payload;
            private int _received;
            private int _sent = -1;
            private long _lastActivity;
        }

        private static long _requestCounter;
        private const int kTimeoutCheckInterval = 10000;
        private readonly IJsonSerializer _serializer;
        private readonly ILogger _logger;
        private readonly TimeSpan _timeout;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, ChunkProcessor> _requests;
        private static readonly ActivitySource kActivity = new(typeof(ChunkMethodInvoker).FullName!);
        private readonly Timer _timer;
    }
}
