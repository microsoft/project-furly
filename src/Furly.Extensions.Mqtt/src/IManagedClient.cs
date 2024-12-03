// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt
{
    using MQTTnet;
    using MQTTnet.Formatter;
    using MQTTnet.Packets;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Message event arguments
    /// </summary>
    public abstract class MqttMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Mqtt message that was received
        /// </summary>
        public abstract MqttApplicationMessage ApplicationMessage { get; }

        /// <summary>
        /// Gets the client identifier.
        /// </summary>
        public abstract string ClientId { get; }

        /// <summary>
        /// Gets or sets whether the library should send
        /// MQTT ACK packets automatically if required.
        /// </summary>
        public bool AutoAcknowledge { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this message was handled.
        /// This value can be used in user code for custom
        /// control flow.
        /// </summary>
        public bool IsHandled { get; set; }

        /// <summary>
        /// Gets the identifier of the MQTT packet
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the reason code which will be sent to the server.
        /// </summary>
        public MqttApplicationMessageReceivedReasonCode ReasonCode { get; set; }

        /// <summary>
        /// Gets or sets the reason string which will be
        /// sent to the server in the ACK packet.
        /// </summary>
        public string? ResponseReasonString { get; set; }

        /// <summary>
        /// Gets or sets the user properties which will be sent to
        /// the server in the ACK packet etc.
        /// </summary>
        public IList<MqttUserProperty> ResponseUserProperties { get; }
            = [];

        /// <summary>
        /// User acknowledge if not auto acknolodging
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public abstract Task AcknowledgeAsync(CancellationToken ct);
    }

    /// <summary>
    /// A managed client interface that is intentionally limited to
    /// publish and subscribe operations. This interface is used by
    /// higher level clients that require the MQTT gestures to
    /// interact with an mqtt broker.
    /// </summary>
    public interface IManagedClient : IAsyncDisposable
    {
        /// <summary>
        /// Returns the client Id used by this client.
        /// </summary>
        /// <remarks>
        /// If a client Id has not been assigned yet by the user
        /// or by the broker, this value is null.
        /// </remarks>
        string? ClientId { get; }

        /// <summary>
        /// The version of the MQTT protocol that this client is using.
        /// </summary>
        MqttProtocolVersion ProtocolVersion { get; }

        /// <summary>
        /// The event that notifies you when this client receives
        /// a PUBLISH from the MQTT broker.
        /// </summary>
        Func<MqttMessageReceivedEventArgs, Task>? MessageReceived { get; set; }

        /// <summary>
        /// Publish a message to the MQTT broker.
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The result of the publish.</returns>
        /// <remarks>
        /// The behavior of publishing when the MQTT client is
        /// disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage message,
            CancellationToken ct = default);

        /// <summary>
        /// Subscribe to a topic on the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the subscribe.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The MQTT broker's response.</returns>
        /// <remarks>
        /// The behavior of subscribing when the MQTT client
        /// is disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientSubscribeResult> SubscribeAsync(
            MqttClientSubscribeOptions options,
            CancellationToken ct = default);

        /// <summary>
        /// Unsubscribe from a topic on the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the unsubscribe
        /// request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The MQTT broker's response.</returns>
        /// <remarks>
        /// The behavior of unsubscribing when the MQTT client is
        /// disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientUnsubscribeResult> UnsubscribeAsync(
            MqttClientUnsubscribeOptions options,
            CancellationToken ct = default);
    }
}
