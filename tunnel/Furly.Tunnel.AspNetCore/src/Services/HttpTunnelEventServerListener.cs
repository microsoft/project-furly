// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Services
{
    using Furly.Tunnel.AspNetCore;
    using Furly.Tunnel.Services;
    using Furly.Exceptions;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Serializers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Binds a event client to the ASP.net core tunnel processor.
    /// </summary>
    public sealed class HttpTunnelEventServerListener : ITunnelListener, IDisposable
    {
        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="servers"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpTunnelEventServerListener(IEnumerable<IEventSubscriber> servers)
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
                var logger = provider.GetRequiredService<ILogger<HttpTunnelEventServer>>();

                var tunnel = new HttpTunnelEventServer(requestDelegate,
                    server, serializer, logger);

                // Wait until started
                tunnel.GetAwaiter().GetResult();
                return tunnel;
            }));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var tunnel in _tunnels)
            {
                tunnel.Dispose();
            }
            _tunnels.Clear();
        }

        private readonly IReadOnlyList<IEventSubscriber> _servers;
        private readonly List<HttpTunnelEventServer> _tunnels = new();
    }
}
