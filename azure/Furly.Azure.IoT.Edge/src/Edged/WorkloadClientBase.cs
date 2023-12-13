// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Workload client
    /// </summary>
    internal abstract class WorkloadClientBase
    {
        /// <summary>
        /// Workload uri
        /// </summary>
        protected Uri WorkloadUri { get; }

        /// <summary>
        /// Api version
        /// </summary>
        protected ApiVersion Version { get; }

        /// <summary>
        /// Module id
        /// </summary>
        protected string ModuleId { get; }

        /// <summary>
        /// Module generation
        /// </summary>
        protected string ModuleGenerationId { get; }

        /// <summary>
        /// Create workload client
        /// </summary>
        /// <param name="httpClientFactory"></param>
        /// <param name="serverUri"></param>
        /// <param name="apiVersion"></param>
        /// <param name="moduleId"></param>
        /// <param name="moduleGenerationId"></param>
        protected WorkloadClientBase(IHttpClientFactory httpClientFactory,
            Uri serverUri, ApiVersion apiVersion, string moduleId, string moduleGenerationId)
        {
            WorkloadUri = serverUri;
            Version = apiVersion;
            ModuleId = moduleId;
            ModuleGenerationId = moduleGenerationId;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Get server certificate
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="expiration"></param>
        /// <returns></returns>
        public abstract Task<ServerCertificateResponse> CreateServerCertificateAsync(
            string hostname, DateTime expiration);

        /// <summary>
        /// Get trust bundle
        /// </summary>
        /// <returns></returns>
        public abstract Task<string> GetTrustBundleAsync();

        /// <summary>
        /// Get manifest trust bundle
        /// </summary>
        /// <returns></returns>
        public abstract Task<string> GetManifestTrustBundleAsync();

        /// <summary>
        /// Encrypt text
        /// </summary>
        /// <param name="initializationVector"></param>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public abstract Task<string> EncryptAsync(string initializationVector, string plainText);

        /// <summary>
        /// Decrypt cipher
        /// </summary>
        /// <param name="initializationVector"></param>
        /// <param name="encryptedText"></param>
        /// <returns></returns>
        public abstract Task<string> DecryptAsync(string initializationVector, string encryptedText);

        /// <summary>
        /// Sign data
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="algorithm"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract Task<string> SignAsync(string keyId, string? algorithm, string data);

        /// <summary>
        /// Run and handle exception
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        protected internal async Task<T> ExecuteAsync<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                return await func.Invoke().ConfigureAwait(false);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                Environment.Exit(ex.ErrorCode);
            }
            catch (Exception ex)
            {
                HandleException(ex, operation);
            }
            return default!;
        }

        /// <summary>
        /// Create http client
        /// </summary>
        /// <returns></returns>
        protected HttpClient GetHttpClient()
        {
            HttpClient client;
            if (WorkloadUri.Scheme == "unix")
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                client = new HttpClient(new SocketsHttpHandler
                {
                    ConnectCallback = async (_, _) =>
                    {
                        var endpoint = new UnixDomainSocketEndPoint(WorkloadUri.LocalPath);
                        var socket = new Socket(AddressFamily.Unix, SocketType.Stream,
                            ProtocolType.Unspecified);
                        await socket.ConnectAsync(endpoint).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                });
#pragma warning restore CA2000 // Dispose objects before losing scope
                client.BaseAddress = new UriBuilder
                {
                    Host = WorkloadUri.Segments.LastOrDefault() ?? "localhost",
                    Scheme = Uri.UriSchemeHttp
                }.Uri;
            }
            else
            {
                client = _httpClientFactory.CreateClient();
                client.BaseAddress = WorkloadUri;
            }
            return client;
        }

        /// <summary>
        /// Handle exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="operation"></param>
        protected abstract void HandleException(Exception ex, string operation);

        /// <inheritdoc/>
        internal class ServerCertificateResponse
        {
            /// <inheritdoc/>
            public string Certificate { get; set; } = null!;

            /// <inheritdoc/>
            public string PrivateKey { get; set; } = null!;
        }

        private readonly IHttpClientFactory _httpClientFactory;
    }
}
