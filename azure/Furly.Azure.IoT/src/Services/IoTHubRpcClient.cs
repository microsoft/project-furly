// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Azure.IoT;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using global::Azure.Identity;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Method client using twin services
    /// </summary>
    public sealed class IoTHubRpcClient : IRpcClient
    {
        /// <inheritdoc/>
        public string Name => "IoTHub";

        /// <inheritdoc/>
        public int MaxMethodPayloadSizeInBytes => 120 * 1024;

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public IoTHubRpcClient(IOptions<IoTHubServiceOptions> options,
            ISerializer serializer, ILogger<IoTHubRpcClient> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                cs.HostName == null)
            {
                throw new ArgumentException("Missing or bad connection string", nameof(options));
            }
            _client = OpenAsync(cs, options.Value);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> CallAsync(string target, string method,
            ReadOnlyMemory<byte> payload, string contentType, TimeSpan? timeout, CancellationToken ct)
        {
            if (!HubResource.Parse(target, out _, out var deviceId, out var moduleId, out var error))
            {
                throw new ArgumentException($"Invalid target {target} provided ({error})");
            }
            var sw = Stopwatch.StartNew();
            try
            {
                var methodInfo = new CloudToDeviceMethod(method)
                {
                    ResponseTimeout = timeout ?? TimeSpan.FromSeconds(kDefaultMethodTimeout)
                };
                if (payload.Length > 0)
                {
                    if (contentType == ContentMimeType.Json)
                    {
                        methodInfo.SetPayloadJson(Encoding.UTF8.GetString(payload.Span));
                    }
                    else
                    {
                        methodInfo.SetPayloadJson(Convert.ToBase64String(payload.Span));
                    }
                }
                var client = await _client.ConfigureAwait(false);
                var result = await (string.IsNullOrEmpty(moduleId) ?
                     client.InvokeDeviceMethodAsync(deviceId, methodInfo, ct) :
                     client.InvokeDeviceMethodAsync(deviceId, moduleId, methodInfo,
                        ct)).ConfigureAwait(false);
                var resultPayload = result.GetPayloadAsJson();

                if (result.Status != 200)
                {
                    _logger.LogDebug("Call {Method} on {Device} ({Module}) with {Payload} " +
                        "returned with error {Status}: {Result} after {Elapsed}",
                        method, deviceId, moduleId, payload, result.Status, resultPayload, sw.Elapsed);
                    MethodCallStatusException.Throw(GetPayload(contentType, resultPayload), _serializer,
                        result.Status);
                }
                _logger.LogDebug("Call {Method} on {Device} ({Module}) took {Elapsed}... ",
                   method, deviceId, moduleId, sw.Elapsed);
                return GetPayload(contentType, resultPayload);
            }
            catch (Exception e) when (e is not MethodCallStatusException)
            {
                _logger.LogDebug(e, "Call {Method} on {Device} ({Module}) failed after {Elapsed}... ",
                    method, deviceId, moduleId, sw.Elapsed);
                throw e.Translate();
            }

            static ReadOnlyMemory<byte> GetPayload(string contentType, string resultPayload)
            {
                if (resultPayload.Length > 0)
                {
                    if (contentType == ContentMimeType.Json)
                    {
                        return Encoding.UTF8.GetBytes(resultPayload);
                    }
                    else
                    {
                        return Convert.FromBase64String(resultPayload);
                    }
                }
                return default;
            }
        }

        /// <summary>
        /// Open service client
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private static async Task<ServiceClient> OpenAsync(ConnectionString connectionString,
            IoTHubServiceOptions options)
        {
            var client = CreateServiceClient(connectionString, options);
            await client.OpenAsync().ConfigureAwait(false);
            return client;

            static ServiceClient CreateServiceClient(ConnectionString connectionString,
               IoTHubServiceOptions options)
            {
                Debug.Assert(!string.IsNullOrEmpty(connectionString.HostName));
                if (string.IsNullOrEmpty(connectionString.SharedAccessKey) ||
                    string.IsNullOrEmpty(connectionString.SharedAccessKeyName))
                {
                    return ServiceClient.Create(connectionString.HostName,
                        new DefaultAzureCredential(options.AllowInteractiveLogin));
                }
                else
                {
                    return ServiceClient.CreateFromConnectionString(connectionString.ToString());
                }
            }
        }

        private readonly ISerializer _serializer;
        private readonly Task<ServiceClient> _client;
        private readonly ILogger _logger;
        private const int kDefaultMethodTimeout = 300; // 5 minutes - default is 30 seconds
    }
}
