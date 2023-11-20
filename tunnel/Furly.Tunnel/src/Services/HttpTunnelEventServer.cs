// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Services
{
    using Furly.Tunnel.Models;
    using Furly.Tunnel.Protocol;
    using Furly.Extensions.Messaging;
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides server side handling of tunnel requests and returns
    /// responses through event client. Used as the cloud side of
    /// a http tunnel that functions fully on top of a messaging plane
    /// such as mqtt.
    /// </summary>
    public sealed class HttpTunnelEventServer : HttpTunnelBaseEventServer
    {
        /// <summary>
        /// Create server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="subscriber"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public HttpTunnelEventServer(ITunnelServer server, IEventSubscriber subscriber,
            IJsonSerializer serializer, ILogger<HttpTunnelEventServer> logger)
            : base(server, subscriber, serializer, logger)
        {
        }

        /// <inheritdoc/>
        protected override async Task RespondAsync(IEventClient responder,
            HttpTunnelResponseModel response, CancellationToken ct = default)
        {
            var buffers = Serializer.SerializeResponse(response, responder.MaxEventPayloadSizeInBytes);
            var requestId = response.RequestId;
            // Send events
            for (var messageId = 0; messageId < buffers.Count; messageId++)
            {
                await responder.SendEventAsync(GetTopicString(
                    HttpTunnelResponseModel.SchemaName, requestId), buffers[messageId],
                    requestId + "_" + messageId.ToString(CultureInfo.InvariantCulture),
                    ct: ct).ConfigureAwait(false);
            }
        }
    }
}
