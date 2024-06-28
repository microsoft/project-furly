// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Services
{
    using Furly.Tunnel.Protocol;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Furly.Tunnel.Exceptions;

    /// <summary>
    /// Provides request routing to module controllers
    /// </summary>
    public sealed class MethodRouter : IRpcHandler, IAwaitable<MethodRouter>,
        IDisposable, IAsyncDisposable
    {
        /// <inheritdoc/>
        public string MountPoint { get; }

        /// <summary>
        /// Property Di to prevent circular dependency between host and controller
        /// </summary>
#pragma warning disable CA1044 // Properties should not be write only
        public IEnumerable<IMethodController> Controllers
#pragma warning restore CA1044 // Properties should not be write only
        {
            set
            {
                foreach (var controller in value)
                {
                    AddToCallTable(controller);
                }
            }
        }

        /// <summary>
        /// Property Di to prevent circular dependency between host and invoker
        /// </summary>
#pragma warning disable CA1044 // Properties should not be write only
        public IEnumerable<IMethodInvoker> ExternalInvokers
#pragma warning restore CA1044 // Properties should not be write only
        {
            set
            {
                foreach (var invoker in value)
                {
                    _chunks.Add(invoker);
                }
            }
        }

        /// <summary>
        /// Create router
        /// </summary>
        /// <param name="servers"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="summarizer"></param>
        /// <param name="options"></param>
        public MethodRouter(IEnumerable<IRpcServer> servers, IJsonSerializer serializer,
            ILogger<MethodRouter> logger, IExceptionSummarizer? summarizer = null,
            IOptions<RouterOptions>? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _summarizer = summarizer;

            MountPoint = options?.Value.MountPoint ?? string.Empty;

            // Create chunk server always
            _chunks = new ChunkMethodServer(_serializer, logger,
                options?.Value.ChunkTimeout ?? TimeSpan.FromSeconds(30), MountPoint);
            _connections = ConnectAsync(servers);
        }

        /// <inheritdoc/>
        public ValueTask<ReadOnlyMemory<byte>> InvokeAsync(string method,
            ReadOnlyMemory<byte> payload, string contentType, CancellationToken ct)
        {
            return _chunks.InvokeAsync(method, payload, contentType, ct);
        }

        /// <inheritdoc/>
        public IAwaiter<MethodRouter> GetAwaiter()
        {
            return _connections.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _chunks.Dispose();
            var connections = await _connections.ConfigureAwait(false);
            foreach (var connection in connections)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create connection to servers
        /// </summary>
        /// <param name="servers"></param>
        /// <returns></returns>
        private async Task<List<IAsyncDisposable>> ConnectAsync(IEnumerable<IRpcServer> servers)
        {
            var disposables = new List<IAsyncDisposable>();
            foreach (var server in servers)
            {
                try
                {
                    var connection = await server.ConnectAsync(this).ConfigureAwait(false);
                    disposables.Add(connection);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect method router to rpc server.");
                }
            }
            if (disposables.Count == 0)
            {
                _logger.LogError("Method router not connected to any rpc server.");
            }
            return disposables;
        }

        /// <summary>
        /// Add target to calltable
        /// </summary>
        /// <param name="target"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void AddToCallTable(object target)
        {
            var versions = target.GetType().GetCustomAttributes<VersionAttribute>(true)
                .Select(v => v.Value)
                .ToList();
            if (versions.Count == 0)
            {
                versions.Add(string.Empty);
            }
            foreach (var methodInfo in target.GetType().GetMethods())
            {
                if (methodInfo.GetCustomAttribute<IgnoreAttribute>() != null)
                {
                    // Should be ignored
                    continue;
                }
                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                {
                    // must be assignable from task
                    continue;
                }
                var tArgs = methodInfo.ReturnParameter.ParameterType
                    .GetGenericArguments();
                if (tArgs.Length > 1)
                {
                    // must have exactly 0 or one (serializable) type to return
                    continue;
                }
                var name = methodInfo.Name;
                if (name.EndsWith("Async", StringComparison.Ordinal))
                {
                    name = name[..^5];
                }

                // Register for all defined versions
                foreach (var version in versions)
                {
                    var versionedName = name + version;
                    if (!_chunks.TryGetValue(versionedName, out var invoker))
                    {
                        invoker = new DynamicInvoker(_logger, name, _summarizer);
                        _chunks.Add(versionedName, invoker);
                    }
                    if (invoker is DynamicInvoker dynamicInvoker)
                    {
                        dynamicInvoker.Add(target, methodInfo, _serializer);
                    }
                    else
                    {
                        // Should never happen...
                        throw new InvalidOperationException(
                            $"Cannot add {versionedName} since invoker is private.");
                    }
                }
            }
        }

        /// <summary>
        /// Encapsulates invoking a matching service on the controller
        /// </summary>
        private class DynamicInvoker : IMethodInvoker
        {
            /// <inheritdoc/>
            public string MethodName { get; private set; }

            /// <summary>
            /// Create dynamic invoker
            /// </summary>
            /// <param name="logger"></param>
            /// <param name="methodName"></param>
            /// <param name="summarizer"></param>
            public DynamicInvoker(ILogger logger, string methodName, IExceptionSummarizer? summarizer)
            {
                MethodName = methodName;
                _logger = logger;
                _summarizer = summarizer;
                _invokers = new List<JsonMethodInvoker>();
            }

            /// <summary>
            /// Add invoker
            /// </summary>
            /// <param name="controller"></param>
            /// <param name="controllerMethod"></param>
            /// <param name="serializer"></param>
            public void Add(object controller, MethodInfo controllerMethod, IJsonSerializer serializer)
            {
                _logger.LogTrace("Adding {Controller}.{Method} method to invoker...",
                    controller.GetType().Name, controllerMethod.Name);
                _invokers.Add(new JsonMethodInvoker(controller, controllerMethod, serializer, _logger, _summarizer));
                MethodName = controllerMethod.Name;
            }

            /// <inheritdoc/>
            public async ValueTask<ReadOnlyMemory<byte>> InvokeAsync(ReadOnlyMemory<byte> payload,
                string contentType, IRpcHandler handler, CancellationToken ct)
            {
                Exception? e = null;
                foreach (var invoker in _invokers)
                {
                    try
                    {
                        return await invoker.InvokeAsync(payload, contentType,
                            handler, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Save last, and continue
                        e = ex;
                    }
                }
                _logger.LogTrace(e, "Exception during method invocation.");
                throw e!;
            }

            private readonly ILogger _logger;
            private readonly IExceptionSummarizer? _summarizer;
            private readonly List<JsonMethodInvoker> _invokers;
        }

        /// <summary>
        /// Invokes a method with json payload
        /// </summary>
        private class JsonMethodInvoker : IMethodInvoker
        {
            /// <inheritdoc/>
            public string MethodName => _controllerMethod.Name;

            /// <summary>
            /// Default filter implementation if none is specified
            /// </summary>
            private sealed class DefaultFilter : ExceptionFilterAttribute
            {
                public override Exception Filter(Exception exception, out int status)
                {
                    status = 400;
                    return exception;
                }
            }

            /// <summary>
            /// Create invoker
            /// </summary>
            /// <param name="controller"></param>
            /// <param name="controllerMethod"></param>
            /// <param name="serializer"></param>
            /// <param name="logger"></param>
            /// <param name="summarizer"></param>
            public JsonMethodInvoker(object controller, MethodInfo controllerMethod,
                IJsonSerializer serializer, ILogger logger, IExceptionSummarizer? summarizer)
            {
                _logger = logger;
                _summarizer = summarizer;
                _serializer = serializer;
                _controller = controller;
                _controllerMethod = controllerMethod;
                _methodParams = _controllerMethod.GetParameters();
                _ef = _controllerMethod.GetCustomAttribute<ExceptionFilterAttribute>(true) ??
                    controller.GetType().GetCustomAttribute<ExceptionFilterAttribute>(true) ??
                    new DefaultFilter();
                var returnArgs = _controllerMethod.ReturnParameter.ParameterType.GetGenericArguments();
                if (returnArgs.Length > 0)
                {
                    _methodTaskContinuation = kMethodResponseAsContinuation.MakeGenericMethod(
                        returnArgs[0]);
                }
            }

            /// <inheritdoc/>
            public async ValueTask<ReadOnlyMemory<byte>> InvokeAsync(ReadOnlyMemory<byte> payload,
                string contentType, IRpcHandler handler, CancellationToken ct)
            {
                object task;
                try
                {
                    object?[] GetInputsArguments()
                    {
                        if (_methodParams.Length == 0)
                        {
                            return Array.Empty<object>();
                        }

                        if (_methodParams.Length == 1)
                        {
                            if (_methodParams[0].ParameterType == typeof(CancellationToken))
                            {
                                return [ct];
                            }
                            var singleParam = _serializer.Deserialize(payload,
                                _methodParams[0].ParameterType);
                            return [singleParam];
                        }

                        if ((_methodParams.Length == 2) &&
                            _methodParams[0].ParameterType != _methodParams[1].ParameterType &&
                            (_methodParams[1].ParameterType == typeof(CancellationToken) ||
                             _methodParams[0].ParameterType == typeof(CancellationToken)))
                        {
                            if (_methodParams[1].ParameterType == typeof(CancellationToken))
                            {
                                var singleParam = _serializer.Deserialize(payload,
                                    _methodParams[0].ParameterType);
                                return [singleParam, ct];
                            }
                            else
                            {
                                var singleParam = _serializer.Deserialize(payload,
                                    _methodParams[1].ParameterType);
                                return [ct, singleParam];
                            }
                        }

                        var data = _serializer.Parse(payload);
                        return _methodParams.Select(param =>
                        {
                            if (param.ParameterType == typeof(CancellationToken))
                            {
                                return ct;
                            }
                            if (data.TryGetProperty(param.Name!, out var value
                                /*, StringComparison.InvariantCultureIgnoreCase*/))
                            {
                                return value.ConvertTo(param.ParameterType);
                            }
                            return param.HasDefaultValue ? param.DefaultValue : null;
                        }).ToArray();
                    }
                    task = _controllerMethod.Invoke(_controller, GetInputsArguments())!;
                }
                catch (Exception e)
                {
                    task = Task.FromException(e);
                }

                if (_methodTaskContinuation == null)
                {
                    return await VoidContinuationAsync((Task)task).ConfigureAwait(false);
                }
                return await ((Task<ReadOnlyMemory<byte>>)_methodTaskContinuation.Invoke(
                    this, [task])!).ConfigureAwait(false);
            }

            /// <summary>
            /// Helper to convert a typed response to buffer or throw appropriate
            /// exception as continuation.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="task"></param>
            /// <returns></returns>
            public Task<ReadOnlyMemory<byte>> MethodResultConverterContinuationAsync<T>(Task<T> task)
            {
                return task.ContinueWith(tr =>
                {
                    if (tr.IsFaulted || tr.IsCanceled)
                    {
                        var ex = tr.Exception?.Flatten().InnerExceptions.FirstOrDefault();
                        ex ??= new TaskCanceledException(tr);
                        _logger.LogTrace(ex, "Method call error");
                        ex = _ef.Filter(ex, out var status);
                        throw ex.AsMethodCallStatusException(status, _summarizer);
                    }
                    return _serializer.SerializeToMemory((object?)tr.Result);
                }, scheduler: TaskScheduler.Default);
            }

            /// <summary>
            /// Helper to convert a void response to buffer or throw appropriate
            /// exception as continuation.
            /// </summary>
            /// <param name="task"></param>
            /// <returns></returns>
            public Task<ReadOnlyMemory<byte>> VoidContinuationAsync(Task task)
            {
                return task.ContinueWith(tr =>
                {
                    if (tr.IsFaulted || tr.IsCanceled)
                    {
                        var ex = tr.Exception?.Flatten().InnerExceptions.FirstOrDefault();
                        ex ??= new TaskCanceledException(tr);
                        _logger.LogTrace(ex, "Method call error");
                        ex = _ef.Filter(ex, out var status);
                        throw ex.AsMethodCallStatusException(status, _summarizer);
                    }
                    return ReadOnlyMemory<byte>.Empty;
                }, scheduler: TaskScheduler.Default);
            }

            private static readonly MethodInfo kMethodResponseAsContinuation =
                typeof(JsonMethodInvoker).GetMethod(nameof(MethodResultConverterContinuationAsync),
                    BindingFlags.Public | BindingFlags.Instance)!;
            private readonly IJsonSerializer _serializer;
            private readonly ILogger _logger;
            private readonly IExceptionSummarizer? _summarizer;
            private readonly object _controller;
            private readonly ParameterInfo[] _methodParams;
            private readonly ExceptionFilterAttribute _ef;
            private readonly MethodInfo _controllerMethod;
            private readonly MethodInfo? _methodTaskContinuation;
        }

        private readonly ILogger _logger;
        private readonly IExceptionSummarizer? _summarizer;
        private readonly IJsonSerializer _serializer;
        private readonly ChunkMethodServer _chunks;
        private readonly Task<List<IAsyncDisposable>> _connections;
    }
}
