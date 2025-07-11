// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Hook trace listener for AioSdk to log to ILogger
    /// </summary>
    internal sealed class AioSdkLogs : TraceListener
    {
        /// <summary>
        /// Create trace hook
        /// </summary>
        /// <param name="listeners"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="defaultListener"></param>
        private AioSdkLogs(List<TraceListener> listeners, ILoggerFactory loggerFactory,
            DefaultTraceListener? defaultListener = null)
        {
            _default = defaultListener;
            _loggerFactory = loggerFactory;
            _defaultLogger = loggerFactory.CreateLogger(nameof(AioSdkLogs));
            _listeners = listeners;
        }

        /// <summary>
        /// Register hook
        /// </summary>
        /// <param name="loggerFactory"></param>
        public static void Hook(ILoggerFactory loggerFactory)
        {
            var listeners = new List<TraceListener>();
            DefaultTraceListener? defaultListener = null;
            foreach (TraceListener listener in Trace.Listeners)
            {
                defaultListener ??= listener as DefaultTraceListener;
                if (listener is not AioSdkLogs and not DefaultTraceListener)
                {
                    listeners.Add(listener);
                }
            }
#pragma warning disable CA2000 // Dispose objects before losing scope
            var hook = new AioSdkLogs(listeners, loggerFactory, defaultListener);
#pragma warning restore CA2000 // Dispose objects before losing scope
            Trace.Listeners.Clear();
            Trace.Listeners.Add(hook);
        }

        /// <inheritdoc/>
        public override void Close()
        {
            _listeners.ForEach(l => l.Close());
            base.Close();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _listeners.ForEach(l => l.Dispose());
            _listeners.Clear();
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override void TraceEvent(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id)
        {
            _listeners.ForEach(l => l.TraceData(eventCache, source, eventType, id));
        }

        /// <inheritdoc/>
        public override void TraceData(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, object? data)
        {
            var logger = GetLogger(source);
#pragma warning disable CA2254 // Template should be a static expression
            logger.Log(MapLevel(source, eventType), data?.ToString());
#pragma warning restore CA2254 // Template should be a static expression
            _listeners.ForEach(l => l.TraceData(eventCache, source, eventType, id, data));
        }

        /// <inheritdoc/>
        public override void TraceData(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, params object?[]? data)
        {
            var logger = GetLogger(source);
#pragma warning disable CA2254 // Template should be a static expression
            logger.Log(MapLevel(source, eventType),
                data != null ? string.Join(", ", data) : string.Empty);
#pragma warning restore CA2254 // Template should be a static expression
            _listeners.ForEach(l => l.TraceData(eventCache, source, eventType, id, data));
        }

        /// <inheritdoc/>
        public override void TraceEvent(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, string? message)
        {
            var logger = GetLogger(source);
#pragma warning disable CA2254 // Template should be a static expression
            logger.Log(MapLevel(source, eventType), message);
#pragma warning restore CA2254 // Template should be a static expression
            _listeners.ForEach(l => l.TraceEvent(eventCache, source, eventType, id, message));
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        /// <inheritdoc/>
        public override void TraceEvent(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, [StringSyntax("CompositeFormat")] string? format,
            params object?[]? args)
        {
            var logger = GetLogger(source);
#pragma warning disable CA2254 // Template should be a static expression
            logger.Log(MapLevel(source, eventType), format ?? "", args ?? []);
#pragma warning restore CA2254 // Template should be a static expression
            _listeners.ForEach(l => l.TraceEvent(eventCache, source, eventType, id, format, args));
            base.TraceEvent(eventCache, source, eventType, id, format, args);
        }

        /// <inheritdoc/>
        public override void TraceTransfer(TraceEventCache? eventCache, string source, int id,
            string? message, Guid relatedActivityId)
        {
            _default?.TraceTransfer(eventCache, source, id, message, relatedActivityId);
            _listeners.ForEach(l => l.TraceTransfer(eventCache, source, id,
                message, relatedActivityId));
            base.TraceTransfer(eventCache, source, id, message, relatedActivityId);
        }

        /// <inheritdoc/>
        public override void Fail(string? message, string? detailMessage)
        {
            _default?.Fail(message, detailMessage);
            _listeners.ForEach(l => l.Fail(message, detailMessage));
        }

        /// <inheritdoc/>
        public override void Fail(string? message)
        {
            _default?.Fail(message);
            _listeners.ForEach(l => l.Fail(message));
        }

        /// <inheritdoc/>
        public override void Write(string? message)
        {
            _builder.Value ??= new StringBuilder();
            _builder.Value.Append(message);
            _listeners.ForEach(l => l.Write(message));
        }

        /// <inheritdoc/>
        public override void WriteLine(string? message)
        {
            _builder.Value ??= new StringBuilder();
            _builder.Value.AppendLine(message);
#pragma warning disable CA2254 // Template should be a static expression
            _defaultLogger.LogDebug(_builder.Value.ToString());
#pragma warning restore CA2254 // Template should be a static expression
            _builder.Value.Clear();
            _listeners.ForEach(l => l.WriteLine(message));
        }

        /// <inheritdoc/>
        public override void Write(object? o)
        {
            _listeners.ForEach(l => l.Write(o));
            base.Write(o);
        }

        /// <inheritdoc/>
        public override void Write(object? o, string? category)
        {
            _listeners.ForEach(l => l.Write(o, category));
            base.Write(o, category);
        }

        /// <inheritdoc/>
        public override void Write(string? message, string? category)
        {
            _listeners.ForEach(l => l.Write(message, category));
            base.Write(message, category);
        }

        /// <inheritdoc/>
        public override void WriteLine(object? o)
        {
            _listeners.ForEach(l => l.WriteLine(o));
            base.WriteLine(o);
        }

        /// <inheritdoc/>
        public override void WriteLine(object? o, string? category)
        {
            _listeners.ForEach(l => l.WriteLine(o, category));
            base.WriteLine(o, category);
        }

        /// <inheritdoc/>
        public override void WriteLine(string? message, string? category)
        {
            _listeners.ForEach(l => l.WriteLine(message, category));
            base.WriteLine(message, category);
        }

        private ILogger GetLogger(string source)
        {
            return _loggers.GetOrAdd(source,
                static (s, factory) => factory.CreateLogger(s), _loggerFactory);
        }

        private static LogLevel MapLevel(string source, TraceEventType eventType)
            => eventType switch
            {
                TraceEventType.Verbose => LogLevel.Debug,
                TraceEventType.Information => LogLevel.Information,
                TraceEventType.Critical => LogLevel.Critical,
                TraceEventType.Error => LogLevel.Error,
                TraceEventType.Warning => LogLevel.Warning,
                _ => LogLevel.Trace
            };

        private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
        private readonly AsyncLocal<StringBuilder> _builder = new();
        private readonly DefaultTraceListener? _default;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _defaultLogger;
        private readonly List<TraceListener> _listeners = new();
    }
}
