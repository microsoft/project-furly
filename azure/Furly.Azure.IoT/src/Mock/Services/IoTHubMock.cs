// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Mock.Services
{
    using Furly.Azure.IoT.Mock.SqlParser;
    using Furly.Azure.IoT.Models;
    using Furly.Exceptions;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Options;
    using Nito.Disposables;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    /// <summary>
    /// Mock device registry
    /// </summary>
    public sealed class IoTHubMock : IIoTHubTwinServices, IIoTHubEventProcessor,
        IRpcClient, IIoTHub, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "IoTHub-Mock";

        /// <inheritdoc/>
        public string HostName { get; } = "mock.azure-devices.net";

        /// <inheritdoc/>
        public IEnumerable<DeviceTwinModel> Devices =>
            _devices.Select(d => d.Device).Where(d => d.ModuleId == null);

        /// <inheritdoc/>
        public IEnumerable<DeviceTwinModel> Modules =>
            _devices.Select(d => d.Device).Where(d => d.ModuleId != null);

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes => 120 * 1024;

        /// <summary>
        /// Create iot hub services
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="options"></param>
        public IoTHubMock(IJsonSerializer serializer,
            IOptions<IoTHubServiceOptions>? options = null) :
            this(options, null, serializer)
        {
        }

        /// <summary>
        /// Create iot hub services
        /// </summary>
        /// <param name="options"></param>
        /// <param name="devices"></param>
        /// <param name="serializer"></param>
        private IoTHubMock(IOptions<IoTHubServiceOptions>? options,
            IEnumerable<DeviceTwinModel>? devices, IJsonSerializer serializer)
        {
            if (options?.Value?.ConnectionString != null)
            {
                if (!ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                    cs.HostName == null)
                {
                    throw new ArgumentException("Bad connection string", nameof(options));
                }
                HostName = cs.HostName;
            }
            if (devices != null)
            {
                _devices.AddRange(devices.Select(device => new IoTHubDeviceState(this, device)));
            }

            _eventHub = Channel.CreateUnbounded<IoTHubEvent>();
            _processor = Task.Factory.StartNew(ProcessEventsAsync, default,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _query = new SqlQuery(this, _serializer);
        }

        /// <summary>
        /// Create iot hub services with devices
        /// </summary>
        /// <param name="devices"></param>
        /// <param name="serializer"></param>
        public static IoTHubMock Create(IEnumerable<DeviceTwinModel> devices,
            IJsonSerializer serializer)
        {
            return new IoTHubMock(null, devices, serializer);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _eventHub.Writer.TryComplete();
            _processor.GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public IIoTHubConnection? Connect(string deviceId,
            string? moduleId = null)
        {
            var model = GetModel(deviceId, moduleId);
            if (model == null)
            {
                return null; // Failed to connect.
            }
            if (model.IsConnected)
            {
                return null; // Already connected
            }
            model.Connect();
            return model;
        }

        /// <inheritdoc/>
        public ValueTask<DeviceTwinModel> CreateOrUpdateAsync(DeviceTwinModel device,
            bool force, CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(device.Id, device.ModuleId);
                if (model == null)
                {
                    // Create
                    model = new IoTHubDeviceState(this, device);
                    _devices.Add(model);
                }
                else if (!force)
                {
                    throw new ResourceConflictException("Twin conflict");
                }
                model.UpdateTwin(device);
                return ValueTask.FromResult(model.Device);
            }
        }

        /// <inheritdoc/>
        public ValueTask<DeviceTwinModel> PatchAsync(DeviceTwinModel device, bool force,
            CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(device.Id, device.ModuleId) ??
                    throw new ResourceNotFoundException("Device twin not found");
                model.UpdateTwin(device);
                return ValueTask.FromResult(model.Device with
                {
                    PrimaryKey = null,
                    SecondaryKey = null,
                    // ...
                });
            }
        }

        /// <inheritdoc/>
        public IDisposable Register(IIoTHubTelemetryHandler listener)
        {
            var id = Guid.NewGuid().ToString();
            _listeners.AddOrUpdate(id, listener);
            return new Disposable(() => _listeners.TryRemove(id, out _));
        }

        /// <inheritdoc/>
        public ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout, CancellationToken ct)
        {
            lock (_lock)
            {
                if (!HubResource.Parse(target, out _, out var deviceId, out var moduleId,
                    out var error))
                {
                    throw new ArgumentException("Target is malformed.", nameof(target));
                }
                var model = GetModel(deviceId, moduleId) ??
                    throw new ResourceNotFoundException("No such device");
                if (!model.IsConnected)
                {
                    throw new TimeoutException("Timed out waiting for device to connect");
                }

                return model.InvokeMethodAsync(method, payload, contentType, ct); // ok to await outside of lock
            }
        }

        /// <inheritdoc/>
        public ValueTask UpdatePropertiesAsync(string deviceId, string? moduleId,
            Dictionary<string, VariantValue> properties, string? etag, CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(deviceId, moduleId, etag) ??
                    throw new ResourceNotFoundException("No such device");
                model.UpdateDesiredProperties(properties);
                return ValueTask.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public ValueTask<DeviceTwinModel> GetAsync(string deviceId, string? moduleId,
            CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(deviceId, moduleId) ??
                    throw new ResourceNotFoundException("No such device");
                return ValueTask.FromResult(model.Device with
                {
                    PrimaryKey = null,
                    SecondaryKey = null,
                });
            }
        }

        /// <inheritdoc/>
        public ValueTask<DeviceTwinModel> GetRegistrationAsync(string deviceId, string? moduleId,
            CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(deviceId, moduleId) ??
                    throw new ResourceNotFoundException("No such device");
                return ValueTask.FromResult(model.Device with
                {
                    Tags = null,
                    Desired = null,
                    Reported = null,
                    // ...
                });
            }
        }

        /// <inheritdoc/>
        public ValueTask DeleteAsync(string deviceId, string? moduleId, string? etag,
            CancellationToken ct)
        {
            lock (_lock)
            {
                var model = GetModel(deviceId, moduleId, etag) ??
                    throw new ResourceNotFoundException("No such device");
                model.Close();
                _devices.RemoveAll(d => d.Device.Id == deviceId && d.Device.ModuleId == moduleId);
                return ValueTask.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public ValueTask<QueryResultModel> QueryAsync(string query, string? continuation,
            int? pageSize, CancellationToken ct)
        {
            if (pageSize < 1)
            {
                pageSize = null;
            }
            pageSize ??= int.MaxValue;
            lock (_lock)
            {
                var result = _query.Query(query).Select(r => r.Copy()).ToList();

                _ = int.TryParse(continuation, out var index);
                var count = Math.Max(0, Math.Min(pageSize.Value, result.Count - index));
                result = result.Skip(index).Take(count).ToList();

                return ValueTask.FromResult(new QueryResultModel
                {
                    ContinuationToken = count >= result.Count ? null :
                        count.ToString(CultureInfo.InvariantCulture),
                    Result = result
                });
            }
        }

        /// <inheritdoc/>
        public ValueTask<DeviceTwinListModel> QueryDeviceTwinsAsync(string query, string? continuation,
            int? pageSize, CancellationToken ct)
        {
            if (pageSize < 1)
            {
                pageSize = null;
            }
            pageSize ??= int.MaxValue;
            lock (_lock)
            {
                var result = _query.Query(query).Select(r => r.Copy()).ToList();

                _ = int.TryParse(continuation, out var index);
                var count = Math.Max(0, Math.Min(pageSize.Value, result.Count - index));
                result = result.Skip(index).Take(count).ToList();

                return ValueTask.FromResult(new DeviceTwinListModel
                {
                    ContinuationToken = count >= result.Count ? null :
                        count.ToString(CultureInfo.InvariantCulture),
                    Items = result.ConvertAll(r => r.ConvertTo<DeviceTwinModel>()!)
                });
            }
        }

        /// <summary>
        /// Get device model
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="etag"></param>
        /// <returns></returns>
        private IoTHubDeviceState? GetModel(string deviceId, string? moduleId,
            string? etag = null)
        {
            var model = _devices.Find(
                t => t.Device.Id == deviceId && t.Device.ModuleId == moduleId);
            if (model != null && etag != null && model.Device.Etag != etag)
            {
                model = null;
            }
            return model;
        }

        /// <summary>
        /// Process events
        /// </summary>
        private async Task ProcessEventsAsync()
        {
            await foreach (var evt in _eventHub.Reader.ReadAllAsync())
            {
                foreach (var listener in _listeners)
                {
                    foreach (var buffer in evt.Buffers)
                    {
                        try
                        {
                            await listener.Value.HandleAsync(
                                evt.DeviceId, evt.ModuleId, evt.Topic ?? string.Empty, buffer,
                                evt.ContentType ?? "application/json",
                                evt.ContentEncoding ?? Encoding.UTF8.WebName,
                                evt.Properties).ConfigureAwait(false);
                        }
                        catch
                        {
                            // should throw here
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Storage record for device and its connection
        /// </summary>
        internal sealed class IoTHubDeviceState : IIoTHubConnection, IRpcServer,
            IEventClient, IKeyValueStore, IDictionary<string, VariantValue>, IProcessIdentity
        {
            /// <inheritdoc/>
            public string Name => "IoTEdge-Mock";

            /// <inheritdoc/>
            public string Identity { get; }

            /// <inheritdoc/>
            string IProcessIdentity.Identity
            {
                get => Device.Id;
            }

            /// <inheritdoc/>
            public IRpcServer RpcServer => this;

            /// <inheritdoc/>
            public IKeyValueStore Twin => this;

            /// <inheritdoc/>
            public IEventClient EventClient => this;

            /// <inheritdoc/>
            public IDictionary<string, VariantValue> State => this;

            /// <inheritdoc/>
            public int MaxEventPayloadSizeInBytes => _outer.MaxMethodPayloadSizeInBytes;

            /// <inheritdoc/>
            public IEnumerable<IRpcHandler> Connected => _handlers.Values;

            /// <inheritdoc/>
            public ICollection<string> Keys => Properties.Keys.ToList();

            /// <inheritdoc/>
            public ICollection<VariantValue> Values => Properties.Values.ToList();

            /// <inheritdoc/>
            public int Count => Properties.Count;

            /// <inheritdoc/>
            public bool IsReadOnly => false;

            /// <inheritdoc/>
            public VariantValue this[string key]
            {
                get => Properties[key];
                set => Add(key, value);
            }

            /// <summary>
            /// Consolidated properties
            /// </summary>
            internal IReadOnlyDictionary<string, VariantValue> Properties
            {
                get
                {
                    lock (_lock)
                    {
                        return Merge(Device.Reported, Device.Desired)
                            ?? new Dictionary<string, VariantValue>();
                    }
                }
            }

            /// <summary>
            /// Device
            /// </summary>
            public DeviceTwinModel Device { get; }

            /// <summary>
            /// Only one client can be connected simultaneously
            /// </summary>
            public bool IsConnected { get; private set; }

            /// <summary>
            /// Create device state
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="device"></param>
            public IoTHubDeviceState(IoTHubMock outer, DeviceTwinModel device)
            {
                _outer = outer;

                Device = device with { };

                // Simulate authentication
                if (Device.PrimaryKey == null)
                {
                    Device.PrimaryKey = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
                }
                if (Device.SecondaryKey == null)
                {
                    Device.SecondaryKey = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
                }

                if (Device.ConnectionState == null)
                {
                    Device.ConnectionState = "Disconnected";
                }
                if (Device.Status == null)
                {
                    Device.Status = "enabled";
                }

                if (Device.StatusUpdatedTime == null)
                {
                    Device.StatusUpdatedTime = DateTime.UtcNow;
                }
                if (Device.LastActivityTime == null)
                {
                    Device.LastActivityTime = DateTime.UtcNow;
                }

                if (Device.Desired == null)
                {
                    Device.Desired = new Dictionary<string, VariantValue>();
                }
                if (Device.Reported == null)
                {
                    Device.Reported = new Dictionary<string, VariantValue>();
                }

                Device.Etag = Guid.NewGuid().ToString();

                Identity = HubResource.Format(_outer.HostName, device.Id, device.ModuleId);
            }

            /// <inheritdoc/>
            public ValueTask<VariantValue?> TryPageInAsync(string key, CancellationToken ct)
            {
                if (!TryGetValue(key, out var value))
                {
                    value = null;
                }
                return ValueTask.FromResult(value);
            }

            /// <inheritdoc/>
            public IEvent CreateEvent()
            {
                return new IoTHubEvent(e =>
                {
                    if (!IsConnected || !_outer._eventHub.Writer.TryWrite(e with { }))
                    {
                        throw new InvalidOperationException(
                            "Cannot send event on disconnected connection.");
                    }
                }, Device.Id, Device.ModuleId);
            }

            /// <inheritdoc/>
            public ValueTask<IAsyncDisposable> ConnectAsync(IRpcHandler server,
                CancellationToken ct)
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException(
                        "Cannot connect server on disconnected connection.");
                }

                var id = Guid.NewGuid().ToString();
                _handlers.AddOrUpdate(id, server);
#pragma warning disable CA2000 // Dispose objects before losing scope
                return ValueTask.FromResult<IAsyncDisposable>(new AsyncDisposable(() =>
                {
                    _handlers.TryRemove(id, out _);
                    return ValueTask.CompletedTask;
                }));
#pragma warning restore CA2000 // Dispose objects before losing scope
            }

            /// <summary>
            /// Connect or disconnect client
            /// </summary>
            internal void Connect()
            {
                lock (_lock)
                {
                    IsConnected = true;
                    Device.ConnectionState = "Connected";
                }
            }

            /// <inheritdoc/>
            public void Close()
            {
                lock (_lock)
                {
                    IsConnected = false;
                    Device.ConnectionState = "Disconnected";
                }
            }

            /// <summary>
            /// Update desired properties
            /// </summary>
            /// <param name="properties"></param>
            public void UpdateDesiredProperties(Dictionary<string, VariantValue> properties)
            {
                lock (_lock)
                {
                    Device.Desired = Merge(Device.Desired, properties);
                    Device.LastActivityTime = DateTime.UtcNow;
                    Device.Etag = Guid.NewGuid().ToString();
                }
            }

            /// <summary>
            /// Update twin
            /// </summary>
            /// <param name="twin"></param>
            internal void UpdateTwin(DeviceTwinModel twin)
            {
                lock (_lock)
                {
                    Device.Tags = Merge(Device.Tags, twin.Tags);
                    Device.Desired = Merge(Device.Desired, twin.Desired);
                    Device.Reported = Merge(Device.Reported, twin.Reported);
                    Device.LastActivityTime = DateTime.UtcNow;
                    Device.Etag = Device.Etag = Guid.NewGuid().ToString();
                }
            }

            /// <summary>
            /// Call method
            /// </summary>
            /// <param name="method"></param>
            /// <param name="bytes"></param>
            /// <param name="contentType"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            internal async ValueTask<ReadOnlyMemory<byte>> InvokeMethodAsync(string method,
                ReadOnlyMemory<byte> bytes, string contentType, CancellationToken ct = default)
            {
                foreach (var handler in _handlers.Values)
                {
                    try
                    {
                        return await handler.InvokeAsync(method, bytes,
                            contentType, ct).ConfigureAwait(false);
                    }
                    catch (NotSupportedException)
                    {
                        // continue;
                    }
                    catch (Exception ex)
                    {
                        throw new MethodCallStatusException(500, ex.Message);
                    }
                }
                throw new MethodCallStatusException(500, "Not supported");
            }

            /// <summary>
            /// Merge properties
            /// </summary>
            /// <param name="target"></param>
            /// <param name="source"></param>
            private static IReadOnlyDictionary<string, VariantValue>? Merge(
                IReadOnlyDictionary<string, VariantValue>? target,
                IReadOnlyDictionary<string, VariantValue>? source)
            {
                if (source == null)
                {
                    return target;
                }

                if (target == null)
                {
                    return source;
                }

                var result = new Dictionary<string, VariantValue>(target);
                foreach (var item in source)
                {
                    if (result.ContainsKey(item.Key))
                    {
                        if (item.Value.IsNull() || item.Value.IsNull())
                        {
                            result.Remove(item.Key);
                        }
                        else
                        {
                            result[item.Key] = item.Value;
                        }
                    }
                    else if (!item.Value.IsNull())
                    {
                        result.Add(item.Key, item.Value);
                    }
                }
                return result;
            }

            /// <inheritdoc/>
            public void Add(string key, VariantValue value)
            {
                if (value.IsNull)
                {
                    // Remove
                    Remove(key);
                    return;
                }
                lock (_lock)
                {
                    Debug.Assert(Device.Reported != null);

                    var reported = new Dictionary<string, VariantValue>(Device.Reported);
                    reported.AddOrUpdate(key, value);
                    Device.Reported = reported;
                    Device.LastActivityTime = DateTime.UtcNow;
                    Device.Etag = Guid.NewGuid().ToString();
                }
            }

            /// <inheritdoc/>
            public bool Remove(string key)
            {
                lock (_lock)
                {
                    Debug.Assert(Device.Reported != null);

                    if (!Device.Reported.ContainsKey(key))
                    {
                        return false;
                    }

                    var reported = new Dictionary<string, VariantValue>(Device.Reported);
                    reported.Remove(key);
                    Device.Reported = reported;
                    Device.LastActivityTime = DateTime.UtcNow;
                    Device.Etag = Guid.NewGuid().ToString();
                    return true;
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    Device.Reported = new Dictionary<string, VariantValue>();
                    Device.LastActivityTime = DateTime.UtcNow;
                    Device.Etag = Guid.NewGuid().ToString();
                }
            }

            /// <inheritdoc/>
            public void Add(KeyValuePair<string, VariantValue> item)
            {
                Add(item.Key, item.Value);
            }

            /// <inheritdoc/>
            public bool ContainsKey(string key)
            {
                return Properties.ContainsKey(key);
            }

            /// <inheritdoc/>
            public bool TryGetValue(string key, [MaybeNullWhen(false)] out VariantValue value)
            {
                return Properties.TryGetValue(key, out value);
            }

            /// <inheritdoc/>
            public bool Contains(KeyValuePair<string, VariantValue> item)
            {
                return Properties.Contains(item);
            }

            /// <inheritdoc/>
            public bool Remove(KeyValuePair<string, VariantValue> item)
            {
                return Remove(item.Key);
            }

            /// <inheritdoc/>
            public IEnumerator<KeyValuePair<string, VariantValue>> GetEnumerator()
            {
                return Properties.GetEnumerator();
            }

            /// <inheritdoc/>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)Properties).GetEnumerator();
            }

            /// <inheritdoc/>
            public void CopyTo(KeyValuePair<string, VariantValue>[] array, int arrayIndex)
            {
                throw new NotSupportedException("Copy to not supported");
            }

            private readonly object _lock = new();
            private readonly IoTHubMock _outer;
            private readonly ConcurrentDictionary<string, IRpcHandler> _handlers = new();
        }

        private readonly object _lock = new();
        private readonly SqlQuery _query;
        private readonly List<IoTHubDeviceState> _devices = new();
        private readonly IJsonSerializer _serializer;
        private readonly Channel<IoTHubEvent> _eventHub;
        private readonly Task _processor;
        private readonly ConcurrentDictionary<string, IIoTHubTelemetryHandler> _listeners = new();
    }
}
