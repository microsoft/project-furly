// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Edged.Version20190130
{
    using Microsoft.Azure.Devices.Edge.Util.Edged.Version20190130.GeneratedCode;
    using Furly.Exceptions;
    using System;
    using System.Net.Http;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <inheritdoc/>
    internal sealed class WorkloadClient : WorkloadClientBase
    {
        /// <inheritdoc/>
        public WorkloadClient(IHttpClientFactory factory, Uri serverUri, ApiVersion apiVersion,
            string moduleId, string moduleGenerationId)
          : base(factory, serverUri, apiVersion, moduleId, moduleGenerationId)
        {
        }

        /// <inheritdoc/>
        public override async Task<ServerCertificateResponse> CreateServerCertificateAsync(
            string hostname, DateTime expiration)
        {
            var request = new ServerCertificateRequest
            {
                CommonName = hostname,
                Expiration = expiration
            };

            using (var httpClient = GetHttpClient())
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = WorkloadUri.ToString()
                };
                var result = await ExecuteAsync(() => edgeletHttpClient.CreateServerCertificateAsync(
                    Version.Name, ModuleId, ModuleGenerationId, request),
                    "CreateServerCertificateAsync").ConfigureAwait(false);
                return new ServerCertificateResponse()
                {
                    Certificate = result.Certificate,
                    PrivateKey = result.PrivateKey.Bytes
                };
            }
        }

        /// <inheritdoc/>
        public override async Task<string> GetTrustBundleAsync()
        {
            using (var httpClient = GetHttpClient())
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = WorkloadUri.ToString()
                };
                var result = await ExecuteAsync(() => edgeletHttpClient.TrustBundleAsync(
                    Version.Name), "TrustBundleAsync").ConfigureAwait(false);
                return result.Certificate;
            }
        }

        /// <inheritdoc/>
        public override Task<string> GetManifestTrustBundleAsync()
        {
            return Task.FromResult(string.Empty);
        }

        /// <inheritdoc/>
        public override async Task<string> EncryptAsync(string initializationVector,
            string plainText)
        {
            var request = new EncryptRequest
            {
                Plaintext = Encoding.UTF8.GetBytes(plainText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (var httpClient = GetHttpClient())
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = WorkloadUri.ToString()
                };
                var result = await ExecuteAsync(() => edgeletHttpClient.EncryptAsync(
                    Version.Name, ModuleId, ModuleGenerationId, request), "Encrypt").ConfigureAwait(false);
                return Convert.ToBase64String(result.Ciphertext);
            }
        }

        /// <inheritdoc/>
        public override async Task<string> DecryptAsync(string initializationVector,
            string encryptedText)
        {
            var request = new DecryptRequest
            {
                Ciphertext = Convert.FromBase64String(encryptedText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (var httpClient = GetHttpClient())
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = WorkloadUri.ToString()
                };
                var result = await ExecuteAsync(() => edgeletHttpClient.DecryptAsync(
                    Version.Name, ModuleId, ModuleGenerationId, request), "Decrypt").ConfigureAwait(false);
                return Encoding.UTF8.GetString(result.Plaintext);
            }
        }

        /// <inheritdoc/>
        public override async Task<string> SignAsync(string keyId, string? algorithm, string data)
        {
            var signRequest = new SignRequest
            {
                KeyId = keyId,
                Algo = SignRequestAlgo.HMACSHA256,
                Data = Encoding.UTF8.GetBytes(data)
            };

            using (var httpClient = GetHttpClient())
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = WorkloadUri.ToString()
                };
                var response = await ExecuteAsync(() => edgeletHttpClient.SignAsync(
                    Version.Name, ModuleId, ModuleGenerationId, signRequest), "SignAsync").ConfigureAwait(false);
                return Convert.ToBase64String(response.Digest);
            }
        }

        /// <inheritdoc/>
        protected override void HandleException(Exception ex, string operation)
        {
            switch (ex)
            {
                case IoTEdgedException<ErrorResponse> errorResponseException:
                    throw new MethodCallStatusException(errorResponseException.StatusCode,
                        $"Error calling {operation}: {errorResponseException.Result?.Message ?? string.Empty}");

                case IoTEdgedException swaggerException:
                    if (swaggerException.StatusCode < 400)
                    {
                        return;
                    }
                    else
                    {
                        throw new MethodCallStatusException(swaggerException.StatusCode,
                            $"Error calling {operation}: {swaggerException.Response ?? string.Empty}");
                    }

                default:
                    ExceptionDispatchInfo.Capture(ex).Throw();
                    break;
            }
        }
    }
}
