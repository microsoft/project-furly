// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using System;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Edgelet client providing discovery and in the future other services
    /// </summary>
    public sealed class IoTEdgeWorkloadApi : IIoTEdgeWorkloadApi
    {
        /// <inheritdoc/>
        public bool IsAvailable => _client != null;

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="client"></param>
        public IoTEdgeWorkloadApi(IHttpClientFactory client)
            : this(client,
                Environment.GetEnvironmentVariable("IOTEDGE_WORKLOADURI"),
                Environment.GetEnvironmentVariable("IOTEDGE_MODULEGENERATIONID"),
                Environment.GetEnvironmentVariable("IOTEDGE_MODULEID"),
                Environment.GetEnvironmentVariable("IOTEDGE_APIVERSION"))
        {
        }

        /// <summary>
        /// Create client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="workloaduri"></param>
        /// <param name="genId"></param>
        /// <param name="moduleId"></param>
        /// <param name="apiVersion"></param>
        public IoTEdgeWorkloadApi(IHttpClientFactory client,
            string? workloaduri, string? genId, string? moduleId, string? apiVersion)
        {
            ArgumentNullException.ThrowIfNull(client);

            if (workloaduri != null && moduleId != null && genId != null)
            {
                apiVersion ??= "2019-01-30";
                var uri = new Uri(workloaduri.TrimEnd('/'));
                _client = GetWorkloadClient(client, uri, apiVersion, apiVersion, moduleId, genId);
            }
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> EncryptAsync(
            string initializationVector, ReadOnlyMemory<byte> plaintext, CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.EncryptAsync(initializationVector,
                Convert.ToBase64String(plaintext.Span)).ConfigureAwait(false);
            return Convert.FromBase64String(result);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> DecryptAsync(
            string initializationVector, ReadOnlyMemory<byte> ciphertext, CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.DecryptAsync(initializationVector,
                Convert.ToBase64String(ciphertext.Span)).ConfigureAwait(false);
            return Convert.FromBase64String(result);
        }

        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> data,
            string? keyId, string? algo, CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.SignAsync(keyId ?? "primary", algo,
                Convert.ToBase64String(data.Span)).ConfigureAwait(false);
            return Convert.FromBase64String(result);
        }

        /// <inheritdoc/>
        public async ValueTask<X509Certificate2Collection> CreateServerCertificateAsync(
            string commonName, DateTime expiration, CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.CreateServerCertificateAsync(
                commonName, expiration).ConfigureAwait(false);
            var collection = new X509Certificate2Collection();
            collection.ImportFromPem(result.Certificate);
            if (collection.Count == 0)
            {
                throw new InvalidOperationException("Certificate is required");
            }
            using (var first = collection[0])
            {
                // Attach private key
                collection[0] = X509Certificate2.CreateFromPem(
                    first.ExportCertificatePem(), result.PrivateKey);
            }
            return collection;
        }

        /// <inheritdoc/>
        public async ValueTask<X509Certificate2Collection> GetTrustBundleAsync(
            CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.GetTrustBundleAsync().ConfigureAwait(false);
            var collection = new X509Certificate2Collection();
            collection.ImportFromPem(result);
            return collection;
        }

        /// <inheritdoc/>
        public async ValueTask<X509Certificate2Collection> GetManifestTrustBundleAsync(
            CancellationToken ct)
        {
            if (_client == null)
            {
                throw new NotSupportedException("Not running in IoT Edge");
            }
            var result = await _client.GetManifestTrustBundleAsync().ConfigureAwait(false);
            var collection = new X509Certificate2Collection();
            collection.ImportFromPem(result);
            return collection;
        }

        /// <summary>
        /// Create workload client
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="workloadUri"></param>
        /// <param name="serverSupportedApiVersion"></param>
        /// <param name="clientSupportedApiVersion"></param>
        /// <param name="moduleId"></param>
        /// <param name="moduleGenerationId"></param>
        /// <returns></returns>
        internal static WorkloadClientBase GetWorkloadClient(IHttpClientFactory factory,
            Uri workloadUri, string serverSupportedApiVersion, string clientSupportedApiVersion,
            string moduleId, string moduleGenerationId)
        {
            var supportedVersion = GetSupportedVersion(serverSupportedApiVersion,
                clientSupportedApiVersion);
            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Microsoft.Azure.Devices.Edge.Util.Edged.Version20180628.WorkloadClient(
                    factory, workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            if (supportedVersion.CompareTo(ApiVersion.Version20190130) >= 0)
            {
                return new Microsoft.Azure.Devices.Edge.Util.Edged.Version20190130.WorkloadClient(
                    factory, workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            if (supportedVersion == ApiVersion.Version20200707)
            {
                return new Microsoft.Azure.Devices.Edge.Util.Edged.Version20200707.WorkloadClient(
                    factory, workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            return new Microsoft.Azure.Devices.Edge.Util.Edged.Version20180628.WorkloadClient(
                factory, workloadUri, supportedVersion, moduleId, moduleGenerationId);

            static ApiVersion GetSupportedVersion(string serverSupportedApiVersion,
                string clientSupportedApiVersion)
            {
                var serverVersion = ApiVersion.ParseVersion(serverSupportedApiVersion);
                var clientVersion = ApiVersion.ParseVersion(clientSupportedApiVersion);

                if (clientVersion == ApiVersion.VersionUnknown)
                {
                    throw new InvalidOperationException(
                        $"Client version {clientSupportedApiVersion} is not supported.");
                }

                if (serverVersion == ApiVersion.VersionUnknown)
                {
                    return clientVersion;
                }

                return serverVersion.Value < clientVersion.Value ? serverVersion : clientVersion;
            }
        }
        private readonly WorkloadClientBase? _client;
    }
}
