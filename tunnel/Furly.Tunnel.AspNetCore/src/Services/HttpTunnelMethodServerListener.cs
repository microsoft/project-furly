// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Services
{
    using Furly.Tunnel.AspNetCore;
    using Furly.Tunnel.Services;
    using Furly.Exceptions;
    using Furly.Extensions.Rpc;
    using Furly.Extensions.Serializers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Binds a rpc server to the ASP.net core tunnel processor.
    /// </summary>
    public sealed class HttpTunnelMethodServerListener : ITunnelListener, IDisposable
    {
        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="servers"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpTunnelMethodServerListener(IEnumerable<IRpcServer> servers)
        {
            _servers = servers?.ToList() ??
                throw new ArgumentNullException(nameof(servers));
        }

        /// <inheritdoc/>
        public void Start(IServiceProvider provider, RequestDelegate request)
        {
            if (_tunnels.Count != 0)
            {
                throw new ResourceInvalidStateException("Listener already started.");
            }

            // Create connector
            var requestDelegate = new HttpTunnelRequestDelegate(provider, request);

            // Connect tunnel
            _tunnels.AddRange(_servers.Select(server =>
            {
                var serializer = provider.GetRequiredService<IJsonSerializer>();
                var logger = provider.GetRequiredService<ILogger<HttpTunnelMethodServer>>();

                var tunnel = new HttpTunnelMethodServer(server, requestDelegate,
                    serializer, logger);

                // Wait until started
                tunnel.GetAwaiter().GetResult();
                return tunnel;
            }));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            // Stop tunnel
            await Task.WhenAll(_tunnels
                .Select(tunnel => tunnel.DisposeAsync().AsTask())).ConfigureAwait(false);
            _tunnels.Clear();
        }

        private readonly IReadOnlyList<IRpcServer> _servers;
        private readonly List<HttpTunnelMethodServer> _tunnels = [];
    }
}
