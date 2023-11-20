// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Edge workload api
    /// </summary>
    public interface IIoTEdgeWorkloadApi
    {
        /// <summary>
        /// Whether the Api is available and usable.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Decrypt cipher text
        /// </summary>
        /// <param name="initializationVector"></param>
        /// <param name="ciphertext"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<ReadOnlyMemory<byte>> DecryptAsync(string initializationVector,
            ReadOnlyMemory<byte> ciphertext, CancellationToken ct = default);

        /// <summary>
        /// Encypt plain text
        /// </summary>
        /// <param name="initializationVector"></param>
        /// <param name="plaintext"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<ReadOnlyMemory<byte>> EncryptAsync(string initializationVector,
            ReadOnlyMemory<byte> plaintext, CancellationToken ct = default);

        /// <summary>
        /// Sign data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="keyId"></param>
        /// <param name="algo"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> data,
            string? keyId = null, string? algo = null, CancellationToken ct = default);

        /// <summary>
        /// Create server certificate
        /// </summary>
        /// <param name="commonName"></param>
        /// <param name="expiration"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<X509Certificate2Collection> CreateServerCertificateAsync(
            string commonName, DateTime expiration, CancellationToken ct = default);

        /// <summary>
        /// Get trust bundle
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<X509Certificate2Collection> GetTrustBundleAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Get manifest trust bundle
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<X509Certificate2Collection> GetManifestTrustBundleAsync(
            CancellationToken ct = default);
    }
}
