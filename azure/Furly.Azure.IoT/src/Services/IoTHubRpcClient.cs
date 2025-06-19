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
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Buffers;
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
        /// <param name="credential"></param>
        /// <param name="logger"></param>
        public IoTHubRpcClient(IOptions<IoTHubServiceOptions> options, ISerializer serializer,
            ICredentialProvider credential, ILogger<IoTHubRpcClient> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _credential = credential;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                cs.HostName == null)
            {
                throw new ArgumentException("Missing or bad connection string", nameof(options));
            }
            _client = OpenAsync(cs);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlySequence<byte>> CallAsync(string target, string method,
            ReadOnlySequence<byte> payload, string contentType, TimeSpan? timeout,
            CancellationToken ct)
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
                        methodInfo.SetPayloadJson(Encoding.UTF8.GetString(payload));
                    }
                    else if (payload.IsSingleSegment)
                    {
                        methodInfo.SetPayloadJson(Convert.ToBase64String(payload.FirstSpan));
                    }
                    else
                    {
                        methodInfo.SetPayloadJson(Convert.ToBase64String(payload.ToArray()));
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
                    _logger.CallReturnedWithError(method, deviceId, moduleId, payload, result.Status, resultPayload, sw.Elapsed);
                    MethodCallStatusException.Throw(GetPayload(contentType, resultPayload), _serializer,
                        result.Status);
                }
                _logger.CallCompleted(method, deviceId, moduleId, sw.Elapsed);
                return new ReadOnlySequence<byte>(GetPayload(contentType, resultPayload));
            }
            catch (Exception e) when (e is not MethodCallStatusException)
            {
                _logger.CallFailed(e, method, deviceId, moduleId, sw.Elapsed);
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
        /// <returns></returns>
        private async Task<ServiceClient> OpenAsync(ConnectionString connectionString)
        {
            var client = CreateServiceClient(connectionString);
            await client.OpenAsync().ConfigureAwait(false);
            return client;
        }

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        private ServiceClient CreateServiceClient(ConnectionString connectionString)
        {
            Debug.Assert(!string.IsNullOrEmpty(connectionString.HostName));
            if (string.IsNullOrEmpty(connectionString.SharedAccessKey) ||
                string.IsNullOrEmpty(connectionString.SharedAccessKeyName))
            {
                return ServiceClient.Create(connectionString.HostName, _credential.Credential);
            }
            else
            {
                return ServiceClient.CreateFromConnectionString(connectionString.ToString());
            }
        }

        private readonly ISerializer _serializer;
        private readonly ICredentialProvider _credential;
        private readonly Task<ServiceClient> _client;
        private readonly ILogger _logger;
        private const int kDefaultMethodTimeout = 300; // 5 minutes - default is 30 seconds
    }

    /// <summary>
    /// Source-generated logging for IoTHubRpcClient
    /// </summary>
    internal static partial class IoTHubRpcClientLogging
    {
        private const int EventClass = 30;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Debug,
            Message = "Call {Method} on {Device} ({Module}) with {Payload} returned " +
            "with error {Status}: {Result} after {Elapsed}")]
        public static partial void CallReturnedWithError(this ILogger logger, string method,
            string device, string? module, object payload, int status, string result, TimeSpan elapsed);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Debug,
            Message = "Call {Method} on {Device} ({Module}) took {Elapsed}... ")]
        public static partial void CallCompleted(this ILogger logger, string method,
            string device, string? module, TimeSpan elapsed);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Debug,
            Message = "Call {Method} on {Device} ({Module}) failed after {Elapsed}... ")]
        public static partial void CallFailed(this ILogger logger, Exception ex, string method,
            string device, string? module, TimeSpan elapsed);
    }
}
