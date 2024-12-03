// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Azure.IoT;
    using Furly.Azure.IoT.Models;
    using Furly.Extensions.Serializers;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of twin services using service sdk.
    /// </summary>
    public sealed class IoTHubServiceClient : IIoTHubTwinServices
    {
        /// <summary>
        /// The host name the client is talking to
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        public IoTHubServiceClient(IOptions<IoTHubServiceOptions> options,
            IJsonSerializer serializer, ICredentialProvider credential,
            ILogger<IoTHubServiceClient> logger)
        {
            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            _credential = credential;

            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                cs.HostName == null)
            {
                throw new ArgumentException("Missing or bad connection string", nameof(options));
            }

            HostName = cs.HostName;
            _registry = OpenAsync(cs);
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceTwinModel> CreateOrUpdateAsync(DeviceTwinModel device,
            bool force, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(device.Id))
            {
                throw new ArgumentException("Missing device id", nameof(device));
            }

            var registry = await _registry.ConfigureAwait(false);

            // First try create device
            try
            {
                var newDevice = await registry.AddDeviceAsync(new Device(device.Id)
                {
                    Scope = device.DeviceScope,
                    Capabilities = device.IotEdge != true ? null : new DeviceCapabilities
                    {
                        IotEdge = device.IotEdge.Value
                    }
                }, ct).ConfigureAwait(false);
            }
            catch (DeviceAlreadyExistsException)
                when (!string.IsNullOrEmpty(device.ModuleId) || force)
            {
                // continue
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Create device failed during registration");
                throw e.Translate();
            }

            // Then update twin assuming it now exists. If fails, retry...
            if (!string.IsNullOrEmpty(device.ModuleId))
            {
                // Try create module
                try
                {
                    var module = await registry.AddModuleAsync(
                        new Module(device.Id, device.ModuleId)
                        {
                            ManagedBy = device.Id,
                        }, ct).ConfigureAwait(false);
                }
                catch (ModuleAlreadyExistsException) when (force)
                {
                    // Expected for update
                }
                catch (Exception e)
                {
                    _logger.LogTrace(e, "Create module failed during registration");
                    throw e.Translate();
                }
            }
            if (!(device.Tags?.Any() ?? false) && !(device.Desired?.Any() ?? false))
            {
                // no twin to create
                return await GetAsync(device.Id, device.ModuleId, ct).ConfigureAwait(false);
            }
            try
            {
                var twin = new Twin(device.Id)
                {
                    ModuleId = device.ModuleId,
                    DeviceId = device.Id,
                    Tags = ToTwinCollection(device.Tags),
                    Properties = new TwinProperties
                    {
                        Desired = ToTwinCollection(device.Desired),
                    }
                };

                // Then update twin assuming it now exists. If fails, retry...
                if (!string.IsNullOrEmpty(device.ModuleId))
                {
                    twin = await registry.UpdateTwinAsync(device.Id, device.ModuleId,
                        twin, "*", ct).ConfigureAwait(false);
                }
                else
                {
                    // Patch device
                    twin = await registry.UpdateTwinAsync(device.Id,
                        twin, "*", ct).ConfigureAwait(false);
                }
                return ToDeviceTwinModel(twin, _serializer, HostName);
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Create Or update failed.");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceTwinModel> PatchAsync(DeviceTwinModel device,
            bool force, CancellationToken ct)
        {
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                var twin = new Twin(device.Id)
                {
                    ModuleId = device.ModuleId,
                    DeviceId = device.Id,
                    Tags = ToTwinCollection(device.Tags),
                    Properties = new TwinProperties
                    {
                        Desired = ToTwinCollection(device.Desired),
                    }
                };
                // Then update twin assuming it now exists. If fails, retry...
                var etag = string.IsNullOrEmpty(device.Etag) || force ? "*" : device.Etag;
                if (!string.IsNullOrEmpty(device.ModuleId))
                {
                    twin = await registry.UpdateTwinAsync(device.Id, device.ModuleId,
                        twin, etag, ct).ConfigureAwait(false);
                }
                else
                {
                    // Patch device
                    twin = await registry.UpdateTwinAsync(device.Id,
                        twin, etag, ct).ConfigureAwait(false);
                }
                return ToDeviceTwinModel(twin, _serializer, HostName);
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Create or update failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceTwinModel> GetAsync(string deviceId, string? moduleId,
            CancellationToken ct)
        {
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                Twin? twin = null;
                if (string.IsNullOrEmpty(moduleId))
                {
                    twin = await registry.GetTwinAsync(deviceId, ct).ConfigureAwait(false);
                    if (twin == null)
                    {
                        throw new DeviceNotFoundException(deviceId);
                    }
                }
                else
                {
                    twin = await registry.GetTwinAsync(deviceId, moduleId, ct).ConfigureAwait(false);
                    if (twin == null)
                    {
                        throw new ModuleNotFoundException(deviceId, moduleId);
                    }
                }
                return ToDeviceTwinModel(twin, _serializer, HostName);
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Get twin failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceTwinModel> GetRegistrationAsync(string deviceId, string? moduleId,
            CancellationToken ct)
        {
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                if (string.IsNullOrEmpty(moduleId))
                {
                    var device = await registry.GetDeviceAsync(deviceId, ct).ConfigureAwait(false);
                    return new DeviceTwinModel
                    {
                        ConnectionState = device.ConnectionState.ToString(),
                        PrimaryKey = device.Authentication?.SymmetricKey?.PrimaryKey,
                        SecondaryKey = device.Authentication?.SymmetricKey?.SecondaryKey,
                        Status = device.Status.ToString(),
                        IotEdge = device.Capabilities?.IotEdge,
                        StatusReason = device.StatusReason,
                        DeviceScope = device.Scope,
                        Etag = device.ETag,
                        Id = device.Id,
                        ModuleId = null,
                        LastActivityTime = device.LastActivityTime,
                        StatusUpdatedTime = device.StatusUpdatedTime,
                        Desired = null,
                        Reported = null,
                        Tags = null,
                        Version = 0
                    };
                }
                else
                {
                    var module = await registry.GetModuleAsync(deviceId, moduleId, ct).ConfigureAwait(false);
                    return new DeviceTwinModel
                    {
                        ConnectionState = module.ConnectionState.ToString(),
                        PrimaryKey = module.Authentication?.SymmetricKey?.PrimaryKey,
                        SecondaryKey = module.Authentication?.SymmetricKey?.SecondaryKey,
                        Status = null,
                        IotEdge = null,
                        StatusReason = null,
                        DeviceScope = null,
                        Etag = module.ETag,
                        Id = module.DeviceId,
                        ModuleId = module.Id,
                        LastActivityTime = module.LastActivityTime,
                        StatusUpdatedTime = module.ConnectionStateUpdatedTime,
                        Desired = null,
                        Reported = null,
                        Tags = null,
                        Version = 0
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Get registration failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<QueryResultModel> QueryAsync(string query, string? continuation,
            int? pageSize, CancellationToken ct)
        {
            if (pageSize < 1)
            {
                pageSize = null;
            }
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrEmpty(continuation))
                {
                    DeserializeContinuationToken(continuation, out query, out continuation,
                        out pageSize);
                }
                var options = new QueryOptions { ContinuationToken = continuation };
                var statement = registry.CreateQuery(query, pageSize);
                var result = await statement.GetNextAsJsonAsync(options).ConfigureAwait(false);
                return new QueryResultModel
                {
                    ContinuationToken = SerializeContinuationToken(query,
                        result.ContinuationToken, pageSize),
                    Result = result.Select(_serializer.Parse).ToList()
                };
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Query failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<DeviceTwinListModel> QueryDeviceTwinsAsync(string query,
            string? continuation, int? pageSize = null, CancellationToken ct = default)
        {
            if (pageSize < 1)
            {
                pageSize = null;
            }
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrEmpty(continuation))
                {
                    DeserializeContinuationToken(continuation, out query, out continuation,
                        out pageSize);
                }
                var options = new QueryOptions { ContinuationToken = continuation };
                var statement = registry.CreateQuery(query, pageSize);
                var result = await statement.GetNextAsTwinAsync(options).ConfigureAwait(false);
                return new DeviceTwinListModel
                {
                    ContinuationToken = SerializeContinuationToken(query,
                        result.ContinuationToken, pageSize),
                    Items = result.Select(s => ToDeviceTwinModel(s, _serializer, HostName)).ToList()
                };
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Query failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask UpdatePropertiesAsync(string deviceId, string? moduleId,
            Dictionary<string, VariantValue> properties, string? etag, CancellationToken ct)
        {
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                var twin = new Twin(deviceId)
                {
                    ModuleId = moduleId,
                    DeviceId = deviceId,
                    Properties = new TwinProperties
                    {
                        Desired = ToTwinCollection(properties),
                    }
                };
                if (string.IsNullOrEmpty(moduleId))
                {
                    var result = await registry.UpdateTwinAsync(deviceId,
                        twin, etag, ct).ConfigureAwait(false);
                }
                else
                {
                    var result = await registry.UpdateTwinAsync(deviceId, moduleId,
                        twin, etag, ct).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Update properties failed ");
                throw e.Translate();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DeleteAsync(string deviceId, string? moduleId, string? etag,
            CancellationToken ct)
        {
            var registry = await _registry.ConfigureAwait(false);
            try
            {
                await (string.IsNullOrEmpty(moduleId) ?
                    registry.RemoveDeviceAsync(new Device(deviceId)
                    {
                        ETag = etag ?? "*"
                    }, ct) :
                    registry.RemoveModuleAsync(new Module(deviceId, moduleId)
                    {
                        ETag = etag ?? "*"
                    }, ct)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Delete failed ");
                throw e.Translate();
            }
        }

        /// <summary>
        /// Open service client
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        internal async Task<RegistryManager> OpenAsync(ConnectionString connectionString)
        {
            try
            {
                var client = CreateRegistryManager(connectionString);
                await client.OpenAsync().ConfigureAwait(false);
                return client;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Creating registry manager failed ");
                throw e.Translate();
            }
        }

        /// <summary>
        /// Create registry manager
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        private RegistryManager CreateRegistryManager(ConnectionString connectionString)
        {
            Debug.Assert(!string.IsNullOrEmpty(connectionString.HostName));
            if (string.IsNullOrEmpty(connectionString.SharedAccessKey) ||
                string.IsNullOrEmpty(connectionString.SharedAccessKeyName))
            {
                return RegistryManager.Create(connectionString.HostName, _credential.Credential);
            }
            else
            {
                return RegistryManager.CreateFromConnectionString(connectionString.ToString());
            }
        }

        /// <summary>
        /// Convert to twin collection
        /// </summary>
        /// <param name="props"></param>
        /// <returns></returns>
        internal TwinCollection? ToTwinCollection(
            IReadOnlyDictionary<string, VariantValue>? props)
        {
            if (props == null)
            {
                return null;
            }
            return new TwinCollection(_serializer.SerializeToString(props));
        }

        /// <summary>
        /// Convert to twin properties model
        /// </summary>
        /// <param name="props"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        internal static IReadOnlyDictionary<string, VariantValue>? ToProperties(
            TwinCollection? props, IJsonSerializer serializer)
        {
            if (props == null)
            {
                return null;
            }
            var model = new Dictionary<string, VariantValue>();
            foreach (KeyValuePair<string, dynamic> item in props)
            {
                model.AddOrUpdate(item.Key,
                    (VariantValue)serializer.FromObject(item.Value));
            }
            return model;
        }

        /// <summary>
        /// Convert twin to device twin model
        /// </summary>
        /// <param name="twin"></param>
        /// <param name="serializer"></param>
        /// <param name="hub"></param>
        /// <returns></returns>
        internal static DeviceTwinModel ToDeviceTwinModel(Twin twin,
            IJsonSerializer serializer, string hub)
        {
            return new DeviceTwinModel
            {
                Id = twin.DeviceId,
                Etag = twin.ETag,
                Hub = hub,
                ModuleId = twin.ModuleId,
                Version = twin.Version,
                ConnectionState = twin.ConnectionState?.ToString(),
                LastActivityTime = twin.LastActivityTime,
                Status = twin.Status?.ToString(),
                StatusReason = twin.StatusReason,
                StatusUpdatedTime = twin.StatusUpdatedTime,
                Tags = ToProperties(twin.Tags, serializer),
                Desired = ToProperties(twin.Properties?.Desired, serializer),
                Reported = ToProperties(twin.Properties?.Reported, serializer),
                IotEdge = twin.Capabilities?.IotEdge,
                DeviceScope = twin.DeviceScope,
                PrimaryKey = null,
                SecondaryKey = null
            };
        }

        /// <summary>
        /// Convert to continuation token string
        /// </summary>
        /// <param name="query"></param>
        /// <param name="continuationToken"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private string? SerializeContinuationToken(string query,
            string continuationToken, int? pageSize)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                return null;
            }
            using (var result = new MemoryStream())
            {
                using (var gs = new GZipStream(result, CompressionMode.Compress))
                {
                    gs.Write(_serializer.SerializeToMemory(new QueryContinuation
                    {
                        PageSize = pageSize,
                        Query = query,
                        Token = continuationToken
                    }).Span);
                }
                return Convert.ToBase64String(result.ToArray());
            }
        }

        /// <summary>
        /// Convert to continuation
        /// </summary>
        /// <param name="token"></param>
        /// <param name="query"></param>
        /// <param name="continuationToken"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private void DeserializeContinuationToken(string token, out string query,
            out string continuationToken, out int? pageSize)
        {
            try
            {
                using (var input = new MemoryStream(Convert.FromBase64String(token)))
                using (var gs = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gs))
                {
                    var result = _serializer.Deserialize<QueryContinuation>(reader.ReadToEnd())
                        ?? throw new FormatException("Decoding token failed");
                    query = result.Query;
                    continuationToken = result.Token;
                    pageSize = result.PageSize;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Malformed continuation token",
                    nameof(continuationToken), ex);
            }
        }

        /// <summary>
        /// Serialize continuations which must include the query to continue
        /// with or else the result will be bogus.
        /// </summary>
        internal sealed record class QueryContinuation
        {
            [DataMember(Name = "q")]
            public string Query { get; set; } = null!;
            [DataMember(Name = "t")]
            public string Token { get; set; } = null!;
            [DataMember(Name = "s")]
            public int? PageSize { get; set; }
        }

        private readonly Task<RegistryManager> _registry;
        private readonly IJsonSerializer _serializer;
        private readonly ICredentialProvider _credential;
        private readonly ILogger _logger;
    }
}
