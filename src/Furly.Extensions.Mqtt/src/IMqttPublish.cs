// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt
{
    using Furly.Extensions.Messaging;
    using MQTTnet;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Chain publishing
    /// </summary>
    internal interface IMqttPublish
    {
        /// <summary>
        /// Publish
        /// </summary>
        /// <param name="message"></param>
        /// <param name="schema"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask PublishAsync(MqttApplicationMessage message,
            IEventSchema? schema, CancellationToken ct);
    }
}
