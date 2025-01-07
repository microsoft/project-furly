// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Mqtt.Clients
{
    using Furly.Extensions.Mqtt;
    using Furly.Extensions.Mqtt.Exceptions;
    using Furly.Extensions.Mqtt.Runtime;
    using Furly.Extensions.Metrics;
    using Furly.Extensions.Rpc;
    using Furly.Exceptions;
    using Microsoft.Extensions.Logging;
    using MQTTnet;
    using MQTTnet.Diagnostics.Logger;
    using MQTTnet.Exceptions;
    using MQTTnet.Formatter;
    using MQTTnet.Protocol;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IdentityModel.Tokens.Jwt;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Managed client wrapper around IMqttClient in mqtt net
    /// </summary>
    internal sealed class MqttSession : IManagedClient, IMqttNetLogger
    {
        /// <inheritdoc/>
        public Func<MqttMessageReceivedEventArgs, Task>? MessageReceived { get; set; }

        /// <inheritdoc/>
        public string? ClientId => UnderlyingMqttClient.Options?.ClientId;

        /// <inheritdoc/>
        public MqttProtocolVersion ProtocolVersion
            => (MqttProtocolVersion)(int)UnderlyingMqttClient.Options.ProtocolVersion;

        /// <inheritdoc/>
        public bool IsEnabled => _logger.IsEnabled(LogLevel.Debug);

        /// <summary>
        /// Connected
        /// </summary>
        public bool IsConnected => UnderlyingMqttClient.IsConnected;

        /// <summary>
        /// Get the maximum packet size that this client can send.
        /// </summary>
        /// <returns>The maximum packet size.</returns>
        public uint MaximumPacketSize { get; private set; }

        /// <summary>
        /// An event that executes every time this client is disconnected.
        /// </summary>
        public Func<MqttClientDisconnectedEventArgs, Task>? Disconnected { get; set; }

        /// <summary>
        /// An event that executes every time this client is connected.
        /// </summary>
        public Func<MqttClientConnectedEventArgs, Task>? Connected { get; set; }

        /// <summary>
        /// Called when session is lost
        /// </summary>
        public Func<MqttClientDisconnectedEventArgs, Task>? SessionLost { get; set; }

        /// <summary>
        /// The MQTT client used by this client to handle all MQTT operations.
        /// </summary>
        internal IMqttClient UnderlyingMqttClient { get; }

        /// <summary>
        /// Create a MQTT session using an MQTT client. The connection and session is
        /// is managed and maintained.
        /// for you.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        /// <param name="meter"></param>
        /// <param name="factory"></param>
        internal MqttSession(ILogger logger, MqttOptions options, IMeterProvider meter,
            Func<IMqttNetLogger, IMqttClient>? factory = null)
        {
            _logger = logger;
            _options = options;
            _metrics = new Metrics(meter.Meter, this);

            UnderlyingMqttClient = factory?.Invoke(this) ??
                new MqttClientFactory().CreateMqttClient(this);

            UnderlyingMqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            UnderlyingMqttClient.DisconnectedAsync += OnDisconnectedAsync;
            UnderlyingMqttClient.ConnectedAsync += OnConnectedAsync;

            Disconnected += OnDisconnectedCoreAsync;

            _pendingReqs = new(_options.MaxPendingMessages ?? uint.MaxValue,
                _options.OverflowStrategy);
            _retryPolicy = _options.ConnectionRetryPolicy ??
                new ExponentialRetry(12, TimeSpan.MaxValue);
        }

        /// <inheritdoc/>
        public void Publish(MqttNetLogLevel logLevel, string source, string message,
            object[] parameters, Exception exception)
        {
#pragma warning disable CA2254 // Template should be a static expression
            _logger.Log(LogLevel.Debug, exception, message, parameters);
#pragma warning restore CA2254 // Template should be a static expression
        }

        /// <summary>
        /// Connect this client and start a clean MQTT session. Once connected,
        /// this client will automatically reconnect as needed and recover the
        /// MQTT session.
        /// </summary>
        /// <param name="options">The details about how to connect to the
        /// MQTT broker.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The CONNACK received from the MQTT broker.</returns>
        /// <remarks>
        /// This operation does not retry by default, but can be configured
        /// to retry. To do so, set the
        /// <see cref="MqttOptions.RetryOnFirstConnect"/> flag and
        /// optionally configure the retry policy
        /// via <see cref="MqttOptions.ConnectionRetryPolicy"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If this method is called
        /// when the client is already managing the connection.</exception>
        public async Task<MqttClientConnectResult> ConnectAsync(
            MqttClientOptions options, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            ct.ThrowIfCancellationRequested();

            if (_isDesiredConnected)
            {
                // should this just return "OK"? Or null since no
                // CONNACK was received?
                throw new InvalidOperationException(
                    "The client is already managing the connection.");
            }

            if (options?.SessionExpiryInterval < 1)
            {
                // This client relies on creating an MQTT session that
                // lasts longer than the initial connection. Otherwise
                // all reconnection attempts will fail to resume the
                // session because the broker already expired it.
                throw new ArgumentException(
                    "Session expiry interval must be greater than 0.");
            }

            ArgumentNullException.ThrowIfNull(options);

            _isClosing = false;
            var connectResult = await MaintainConnectionAsync(options, null,
                ct).ConfigureAwait(false);

            Debug.Assert(connectResult != null);
            _isDesiredConnected = true;
            _logger.LogInformation("Successfully connected the session client to" +
                " the MQTT broker. This connection will now be maintained.");

            return connectResult;
        }

        /// <summary>
        /// Disconnect this client and end the MQTT session.
        /// </summary>
        /// <param name="options">The optional parameters that can be sent in the
        /// DISCONNECT packet to the MQTT broker.</param>
        /// <param name="ct">The cancellation token.</param>
        public async Task DisconnectAsync(MqttClientDisconnectOptions? options,
            CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ct.ThrowIfCancellationRequested();

            if (options != null && options.SessionExpiryInterval != 0)
            {
                // This method should only be called when the session is no
                // longer needed. By providing a non-zero value, you are
                // trying to keep the session alive on the broker.
                throw new ArgumentException("Cannot disconnect when session was " +
                    "configured with a non-zero session expiry interval");
            }

            options ??= new MqttClientDisconnectOptions();
            options.SessionExpiryInterval = 0;

            _isClosing = true;
            var cts = _reconnectCts;
            if (cts != null)
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            await DisconnectCoreAsync(options, ct).ConfigureAwait(false);

            var disconnectedArgs = new MqttClientDisconnectedEventArgs(true,
                null, MqttClientDisconnectReason.NormalDisconnection,
                null, null, null);

            var e = new MqttSessionExpiredException("The queued request cannot be " +
                "completed now that the session client has been closed by the user.");
            await FinalizeSessionAsync(e, disconnectedArgs, ct).ConfigureAwait(false);
            StopPublishingSubscribingAndUnsubscribing();
            _logger.LogInformation("Successfully disconnected the session client from" +
                " the MQTT broker. This connection will no longer be maintained.");
        }

        /// <inheritdoc/>
        public async Task<MqttClientPublishResult> PublishAsync(
            MqttApplicationMessage message, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ct.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<MqttClientPublishResult>();
            var queuedRequest = Request.Create(message, tcs, ct);
            ct.Register(async () =>
            {
                try
                {
                    await _pendingReqs.RemoveAsync(queuedRequest,
                        default).ConfigureAwait(false);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Failed to remove a queued publish " +
                        "because the session client was already disposed.");
                }
            });

            await _pendingReqs.AddLastAsync(queuedRequest,
                ct).ConfigureAwait(false);

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MqttClientSubscribeResult> SubscribeAsync(
            MqttClientSubscribeOptions options, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ct.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<MqttClientSubscribeResult>();
            var queuedRequest = Request.Create(options, tcs, ct);
            ct.Register(async () =>
            {
                try
                {
                    await _pendingReqs.RemoveAsync(queuedRequest,
                        default).ConfigureAwait(false);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Failed to remove a queued subscribe " +
                        "because the session client was already disposed.");
                }
            });

            await _pendingReqs.AddLastAsync(queuedRequest, ct).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(
            MqttClientUnsubscribeOptions options, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ct.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<MqttClientUnsubscribeResult>();
            var queuedRequest = Request.Create(options, tcs, ct);
            ct.Register(async () =>
            {
                try
                {
                    await _pendingReqs.RemoveAsync(queuedRequest,
                        default).ConfigureAwait(false);
                    tcs.TrySetCanceled();
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Failed to remove a queued unsubscribe " +
                        "because the session client was already disposed.");
                }
            });

            await _pendingReqs.AddLastAsync(queuedRequest, ct).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            try
            {
                Disconnected -= OnDisconnectedCoreAsync;
                if (IsConnected || _isDesiredConnected)
                {
                    await DisconnectAsync(null, default).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Error while disconnecting in dispose");
            }
            finally
            {
                _workerCts?.Dispose();
                _reconnectCts?.Dispose();
                _disconnectedEventLock.Dispose();
                _pendingReqs.Dispose();

                UnderlyingMqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                UnderlyingMqttClient.DisconnectedAsync -= OnDisconnectedAsync;
                UnderlyingMqttClient.ConnectedAsync -= OnConnectedAsync;

                _tokenRefresh?.Dispose();
                UnderlyingMqttClient.Dispose();
                _ackSenderCts.Dispose();
                _pendingAcks.Dispose();

                _metrics.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Send extended auth data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task SendEnhancedAuthenticationExchangeDataAsync(
            MqttEnhancedAuthenticationExchangeData data, CancellationToken ct)
        {
            return UnderlyingMqttClient.SendEnhancedAuthenticationExchangeDataAsync(
                data, ct);
        }

        /// <summary>
        /// Reconnect
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Task ReconnectAsync(CancellationToken ct)
        {
            if (UnderlyingMqttClient.Options == null)
            {
                throw new InvalidOperationException(
                    "Cannot reconnect prior to the initial connect");
            }
            return ConnectCoreAsync(UnderlyingMqttClient.Options, ct);
        }

        /// <summary>
        /// Connect the client using the underlying client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        internal async Task<MqttClientConnectResult> ConnectCoreAsync(
            MqttClientOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            MaximumPacketSize = options.MaximumPacketSize;

            var result = await UnderlyingMqttClient.ConnectAsync(options,
                ct).ConfigureAwait(false);

            _metrics.Connect.Add(1, KeyValuePair.Create("ResultCode",
                (object?)result.ResultCode));

            if (options.AuthenticationMethod == "K8S-SAT" &&
                _options.SatAuthFile != null)
            {
                _tokenRefresh?.Dispose();
                _tokenRefresh = new TokenRefreshTimer(this, _options.SatAuthFile);
            }

            // A successful connect attempt should always return a non-null connect result
            Debug.Assert(result != null);

            if (string.IsNullOrEmpty(UnderlyingMqttClient.Options.ClientId))
            {
                UnderlyingMqttClient.Options.ClientId = result.AssignedClientIdentifier;
            }

            return result;
        }

        /// <summary>
        /// Disconnect this client from the MQTT broker.
        /// </summary>
        /// <param name="options">The optional parameters to include in the
        /// DISCONNECT request.</param>
        /// <param name="ct">Cancellation token.</param>
        internal Task DisconnectCoreAsync(MqttClientDisconnectOptions? options,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_ctsLockObj)
            {
                StopAcknowledgingReceivedMessages();
            }

            return UnderlyingMqttClient.DisconnectAsync(
                options ?? new MqttClientDisconnectOptions(), ct);
        }

        /// <summary>
        /// Perform a publish using the underlying client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        internal async Task<MqttClientPublishResult> PublishCoreAsync(
            MqttApplicationMessage message, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            await ValidateMessageSize(message).ConfigureAwait(false);
            return await UnderlyingMqttClient.PublishAsync(
                message, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Perform a subscribe using the underlying client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        internal async Task<MqttClientSubscribeResult> SubscribeCoreAsync(
            MqttClientSubscribeOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return await UnderlyingMqttClient.SubscribeAsync(
                options, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Perform a unsubscribe using the underlying client
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        internal async Task<MqttClientUnsubscribeResult> UnsubscribeCoreAsync(
            MqttClientUnsubscribeOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return await UnderlyingMqttClient.UnsubscribeAsync(
                options, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal disconnect handler
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task OnDisconnectedCoreAsync(MqttClientDisconnectedEventArgs args)
        {
            //
            // MQTTNet's client often triggers the same "OnDisconnect" callback
            // more times than expected, so only start reconnection once
            //
            await _disconnectedEventLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_isDesiredConnected)
                {
                    return;
                }
                if (IsConnected)
                {
                    _logger.LogInformation(
                        "Disconnect reported by MQTTnet client, but it was already handled");
                    return;
                }

                StopPublishingSubscribingAndUnsubscribing();

                // It is important to stop the pub/sub/unsub threads before resetting
                // these message states.
                // If you reset the message states first, then pubs/subs/unsubs may be
                // dequeued into about-to-be-cancelled threads.
                await ResetMessagesStatesAsync(default).ConfigureAwait(false);

                if (IsFatal(args.Reason))
                {
                    _logger.LogInformation("Disconnect detected and it was due to fatal error. " +
                        "The client will not attempt to reconnect. Disconnect reason: {Reason}",
                        args.Reason);
                    var retryException = new ResourceExhaustionException(
                        "A fatal error was encountered while trying to re-establish the session, " +
                        "so this request cannot be completed.", args.Exception);
                    await FinalizeSessionAsync(retryException, args, default).ConfigureAwait(false);
                    return;
                }

                _logger.LogInformation(
                    "Disconnect detected, starting reconnection. Disconnect reason: {Reason}",
                    args.Reason);

                var options = UnderlyingMqttClient.Options;

                // This field is set when connecting, and this function should only
                // be called after connecting.
                Debug.Assert(options != null);

                _reconnectCts?.Dispose();
                _reconnectCts = new();

                // start reconnection if the user didn't initiate this disconnect
                await MaintainConnectionAsync(options, args,
                    _reconnectCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _disconnectedEventLock.Release();
            }
        }

        /// <summary>
        /// Maintain the connection to the broker.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="lastDisconnect"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<MqttClientConnectResult?> MaintainConnectionAsync(
            MqttClientOptions options, MqttClientDisconnectedEventArgs? lastDisconnect,
            CancellationToken ct)
        {
            // This function is either called when initially connecting the
            // client or when reconnecting it. The behavior
            // of this function should change depending on the context it
            // was called. For instance, thrown exceptions are the expected
            // behavior when called from the initial ConnectAsync thread,
            // but any exceptions thrown in the reconnection thread will be
            // unhandled and may crash the client.
            var isReconnection = lastDisconnect != null;
            uint attemptCount = 1;
            MqttClientConnectResult? mostRecentConnectResult = null;
            var lastException = lastDisconnect?.Exception;
            var retryDelay = TimeSpan.Zero;

            while (true)
            {
                // This flag signals that the user is trying to close the connection.
                // If this happens when the client is reconnection, simply abandon
                // reconnecting and end this task.
                if (_isClosing && isReconnection)
                {
                    return null;
                }
                else if (_isClosing && lastException != null)
                {
                    // If the user disconnects the client while they were trying to
                    // connect it, stop trying to connect it and just report the most
                    // recent error.
                    throw lastException;
                }

                if (!isReconnection && attemptCount > 1 &&
                    _options.RetryOnFirstConnect == false)
                {
                    Debug.Assert(lastException != null);
                    throw lastException;
                }

                if (IsFatal(lastException!, _reconnectCts?.Token.IsCancellationRequested
                        ?? ct.IsCancellationRequested))
                {
                    _logger.LogError(lastException,
                        "Encountered a fatal exception while maintaining connection");
                    if (isReconnection)
                    {
                        var retryException = new ResourceExhaustionException(
                            "A fatal error was encountered while trying to re-establish the" +
                            " session, so this request cannot be completed.", lastException!);

                        // This function was called to reconnect after an unexpected disconnect.
                        // Since the error is fatal, notify the user via callback that the
                        // client has crashed, but don't throw the exception since this task
                        // is unmonitored.
                        await FinalizeSessionAsync(retryException, lastDisconnect!,
                            ct).ConfigureAwait(false);
                        return null;
                    }
                    else
                    {
                        // This function was called directly by the user via
                        // ConnectAsync, so just throw the exception.
                        throw lastException!;
                    }
                }

                // Always consult the retry policy when reconnecting, but only
                // consult it on attempt > 1 when initially connecting
                if ((isReconnection || attemptCount > 1)
                    && !_retryPolicy.ShouldRetry(attemptCount, lastException!, out retryDelay))
                {
                    _logger.LogError(lastException,
                        "Retry policy was exhausted while trying to maintain a connection");
                    var retryException = new ResourceExhaustionException(
                        "Retry policy has been exhausted. See inner exception for the " +
                        "latest exception encountered while retrying.", lastException!);

                    if (lastDisconnect != null)
                    {
                        // This function was called to reconnect after an unexpected
                        // disconnect. Since the error is fatal, notify the user via
                        // callback that the client has crashed, but don't throw the
                        // exception since this task is unmonitored.
                        var disconnectedEventArgs = new MqttClientDisconnectedEventArgs(
                            lastDisconnect.ClientWasConnected, mostRecentConnectResult,
                            lastDisconnect.Reason, lastDisconnect.ReasonString,
                            lastDisconnect.UserProperties, retryException);

                        await FinalizeSessionAsync(retryException, disconnectedEventArgs,
                            ct).ConfigureAwait(false);
                        return null;
                    }
                    else
                    {
                        // This function was called directly by the user via
                        // ConnectAsync, so just throw the exception.
                        throw retryException;
                    }
                }

                // With all the above conditions checked, the client should attempt
                // to connect again after a delay
                try
                {
                    if (retryDelay.CompareTo(TimeSpan.Zero) > 0)
                    {
                        _logger.LogInformation("Waiting {RetryDelay} before next reconnect attempt",
                            retryDelay);
                        await Task.Delay(retryDelay, ct).ConfigureAwait(false);
                    }

                    ct.ThrowIfCancellationRequested();
                    _logger.LogInformation("Trying to connect. Attempt number {AttemptCount}",
                        attemptCount);

                    if (isReconnection || _options.RetryOnFirstConnect != false)
                    {
                        using var reconnectionTimeoutCancellationToken = new CancellationTokenSource();
                        reconnectionTimeoutCancellationToken.CancelAfter(
                            _options.ConnectionAttemptTimeout ?? TimeSpan.FromSeconds(30));
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                            ct, reconnectionTimeoutCancellationToken.Token);
                        mostRecentConnectResult = await TryEstablishConnectionAsync(options,
                            cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        mostRecentConnectResult = await TryEstablishConnectionAsync(options,
                            ct).ConfigureAwait(false);
                    }

                    // If the connection was re-established, but the session was lost, report
                    // it as a fatal error to the user and disconnect from the broker.
                    if (mostRecentConnectResult != null
                        && mostRecentConnectResult.ResultCode == MqttClientConnectResultCode.Success
                        && isReconnection
                        && !mostRecentConnectResult.IsSessionPresent)
                    {
                        var disconnectedArgs = new MqttClientDisconnectedEventArgs(true, null,
                            MqttClientDisconnectReason.NormalDisconnection,
                            "The session client re-established the connection, but the " +
                                "MQTT broker no longer had the session.",
                            null, new MqttSessionExpiredException());

                        var queuedItemException = new MqttSessionExpiredException(
                            "The queued request has been cancelled because the session " +
                            "is no longer present");
                        await FinalizeSessionAsync(queuedItemException, disconnectedArgs,
                            ct).ConfigureAwait(false);
                        _logger.LogError("Reconnection succeeded, but the session was " +
                            "lost so the client closed the connection.");

                        await FinalizeSessionAsync(queuedItemException, disconnectedArgs,
                            ct).ConfigureAwait(false);

                        //
                        // The provided cancellation token will be cancelled while
                        // disconnecting, so don't pass it along
                        //
                        await DisconnectAsync(null, default).ConfigureAwait(false);
                        // Reconnection should end because the session was lost
                        return null;
                    }

                    if (isReconnection)
                    {
                        _logger.LogInformation("Reconnection finished after successfully " +
                            "connecting to the MQTT broker again and re-joining the existing " +
                            "MQTT session.");
                    }

                    if (mostRecentConnectResult != null && mostRecentConnectResult.ResultCode
                        == MqttClientConnectResultCode.Success)
                    {
                        StartPublishingSubscribingAndUnsubscribing();
                    }
                    return mostRecentConnectResult;
                }
                catch (Exception) when (_isClosing && isReconnection)
                {
                    // This happens when reconnecting if the user attempts to
                    // manually disconnect the session client. When that
                    // happens, we simply want to end the reconnection logic
                    // and let the thread end without throwing.

                    _logger.LogInformation("Session client reconnection cancelled " +
                        "because the client is being closed.");
                    return null;
                }
                catch (Exception e)
                {
                    lastException = e;
                    _logger.LogWarning(e, "Encountered an exception while connecting. " +
                        "May attempt to reconnect.");
                }

                attemptCount++;
            }
        }

        /// <summary>
        /// Try connect
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="ConnectingFailedException"></exception>
        private async Task<MqttClientConnectResult?> TryEstablishConnectionAsync(
            MqttClientOptions options, CancellationToken ct)
        {
            if (IsConnected)
            {
                return null;
            }

            // When reconnecting, never use a clean session. This client wants
            // to recover the session and the connection.
            if (_isDesiredConnected)
            {
                options.CleanSession = false;
            }

            var connectResult = await ConnectCoreAsync(options, ct).ConfigureAwait(false);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new ConnectingFailedException("Client tried to connect but " +
                    $"server denied connection with reason '{connectResult.ResultCode}'.",
                    connectResult);
            }

            return connectResult;
        }

        /// <summary>
        /// Start the request queue worker
        /// </summary>
        private void StartPublishingSubscribingAndUnsubscribing()
        {
            lock (_ctsLockObj)
            {
                if (!_disposed)
                {
                    _logger.LogInformation("Starting the session client's worker thread");
                    _ = Task.Run(() => ExecuteQueuedItemsAsync(
                        _workerCts.Token),
                        _workerCts.Token);
                }
            }
        }

        /// <summary>
        /// Stop the request queue worker
        /// </summary>
        private void StopPublishingSubscribingAndUnsubscribing()
        {
            lock (_ctsLockObj)
            {
                try
                {
                    _logger.LogInformation("Stopping the session client's worker thread");
                    _workerCts.Cancel(false);
                    _workerCts.Dispose();
                    _workerCts = new();
                }
                catch (ObjectDisposedException)
                {
                    // The object was already disposed prior to this method being called
                }
            }
        }

        /// <summary>
        /// Reset all messages pending to not sent
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ResetMessagesStatesAsync(CancellationToken ct)
        {
            _logger.LogInformation("Resetting the state of all queued messages");
            await _pendingReqs.MarkMessagesAsUnsentAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// While connected send the queued requests that are pending.
        /// </summary>
        /// <param name="disconnected"></param>
        /// <returns></returns>
        private async Task ExecuteQueuedItemsAsync(CancellationToken disconnected)
        {
            try
            {
                while (IsConnected)
                {
                    var queuedRequest = await _pendingReqs.PeekNextUnsentAsync(
                        disconnected).ConfigureAwait(false);
                    disconnected.ThrowIfCancellationRequested();

                    // This request can either be cancelled because the connection was lost
                    // or because the user cancelled this specific request
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                        disconnected, queuedRequest.CancellationToken);

                    await DispatchAsync(queuedRequest, cts,
                        disconnected).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Publish message task cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Error while publishing queued application messages.");
            }
            finally
            {
                _logger.LogInformation("Stopped publishing messages.");
            }
        }

        /// <summary>
        /// Dispatch the queued requests to the appropriate handler
        /// </summary>
        /// <param name="queuedRequest"></param>
        /// <param name="cts"></param>
        /// <param name="connectionLostCancellationToken"></param>
        /// <returns></returns>
        private async Task DispatchAsync(Request queuedRequest, CancellationTokenSource cts,
            CancellationToken connectionLostCancellationToken)
        {
            switch (queuedRequest)
            {
                case Request<MqttApplicationMessage, MqttClientPublishResult> pub:
                    _ = ExecuteSinglePublishAsync(pub, cts.Token);
                    break;
                case Request<MqttClientSubscribeOptions, MqttClientSubscribeResult> sub:
                    _ = ExecuteSingleSubscribeAsync(sub, cts.Token);
                    break;
                case Request<MqttClientUnsubscribeOptions, MqttClientUnsubscribeResult> unsub:
                    _ = ExecuteSingleUnsubscribeAsync(unsub, cts.Token);
                    break;
                default:
                    // This should never happen since the queue should only contain pubs,
                    // subs, and unsubs
                    _logger.LogError("Unrecognized queued item. Discarding it.");
                    await _pendingReqs.RemoveAsync(queuedRequest,
                        connectionLostCancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Publish handler
        /// </summary>
        /// <param name="queuedPublish"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ExecuteSinglePublishAsync(
            Request<MqttApplicationMessage, MqttClientPublishResult> queuedPublish,
            CancellationToken ct)
        {
            try
            {
                var publishResult = await PublishCoreAsync(queuedPublish.Options,
                    ct).ConfigureAwait(false);

                _metrics.Publish.Add(1, KeyValuePair.Create("ResultCode",
                    (object?)publishResult.ReasonCode));

                await _pendingReqs.RemoveAsync(queuedPublish, default).ConfigureAwait(false);
                if (!queuedPublish.ResultTaskCompletionSource.TrySetResult(publishResult))
                {
                    _logger.LogError("Failed to set task completion source for publish request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedPublish.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _pendingReqs.RemoveAsync(queuedPublish, default).ConfigureAwait(false);
                    queuedPublish.ResultTaskCompletionSource.TrySetCanceled(default);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, queuedPublish.CancellationToken.IsCancellationRequested))
                {
                    await _pendingReqs.RemoveAsync(queuedPublish, default).ConfigureAwait(false);
                    if (!queuedPublish.ResultTaskCompletionSource.TrySetException(e))
                    {
                        _logger.LogError("Failed to set task completion source for publish request");
                    }
                }
            }
        }

        /// <summary>
        /// Subscribe handler
        /// </summary>
        /// <param name="queuedSubscribe"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ExecuteSingleSubscribeAsync(
            Request<MqttClientSubscribeOptions, MqttClientSubscribeResult> queuedSubscribe,
            CancellationToken ct)
        {
            try
            {
                var subscribeResult = await SubscribeCoreAsync(queuedSubscribe.Options,
                    ct).ConfigureAwait(false);

                foreach (var item in subscribeResult.Items)
                {
                    _metrics.Subscribe.Add(1, KeyValuePair.Create("ResultCode", (object?)item.ResultCode));
                }

                await _pendingReqs.RemoveAsync(queuedSubscribe, default).ConfigureAwait(false);
                if (!queuedSubscribe.ResultTaskCompletionSource.TrySetResult(subscribeResult))
                {
                    _logger.LogError("Failed to set task completion source for subscribe request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedSubscribe.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _pendingReqs.RemoveAsync(queuedSubscribe, default).ConfigureAwait(false);
                    queuedSubscribe.ResultTaskCompletionSource.TrySetCanceled(default);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, queuedSubscribe.CancellationToken.IsCancellationRequested))
                {
                    await _pendingReqs.RemoveAsync(queuedSubscribe, default).ConfigureAwait(false);
                    if (!queuedSubscribe.ResultTaskCompletionSource.TrySetException(e))
                    {
                        _logger.LogError("Failed to set task completion source for subscribe request");
                    }
                }
            }
        }

        /// <summary>
        /// Unsubscribe handler
        /// </summary>
        /// <param name="queuedUnsubscribe"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ExecuteSingleUnsubscribeAsync(
            Request<MqttClientUnsubscribeOptions, MqttClientUnsubscribeResult> queuedUnsubscribe,
            CancellationToken ct)
        {
            try
            {
                var unsubscribeResult = await UnsubscribeCoreAsync(queuedUnsubscribe.Options,
                    ct).ConfigureAwait(false);

                foreach (var item in unsubscribeResult.Items)
                {
                    _metrics.Unsubscribe.Add(1, KeyValuePair.Create("ResultCode", (object?)item.ResultCode));
                }

                await _pendingReqs.RemoveAsync(queuedUnsubscribe, default).ConfigureAwait(false);
                if (!queuedUnsubscribe.ResultTaskCompletionSource.TrySetResult(unsubscribeResult))
                {
                    _logger.LogError("Failed to set task completion source for unsubscribe request");
                }
            }
            catch (OperationCanceledException)
            {
                if (queuedUnsubscribe.CancellationToken.IsCancellationRequested)
                {
                    // User cancelled this request
                    await _pendingReqs.RemoveAsync(queuedUnsubscribe, default).ConfigureAwait(false);
                    queuedUnsubscribe.ResultTaskCompletionSource.TrySetCanceled(default);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e, queuedUnsubscribe.CancellationToken.IsCancellationRequested))
                {
                    await _pendingReqs.RemoveAsync(queuedUnsubscribe, default).ConfigureAwait(false);
                    if (!queuedUnsubscribe.ResultTaskCompletionSource.TrySetException(e))
                    {
                        _logger.LogError("Failed to set task completion source for unsubscribe request");
                    }
                }
            }
        }

        private async Task FinalizeSessionAsync(Exception queuedItemException,
            MqttClientDisconnectedEventArgs disconnectedEventArgs, CancellationToken ct)
        {
            if (!_isDesiredConnected)
            {
                return;
            }
            _isDesiredConnected = false;

            SessionLost?.Invoke(disconnectedEventArgs);
            await _pendingReqs.CancelAsync(queuedItemException,
                ct).ConfigureAwait(false);
        }

        // These reason codes are fatal if the broker sends a DISCONNECT packet with this reason.
        private static bool IsFatal(MqttClientDisconnectReason code)
        {
            switch (code)
            {
                case MqttClientDisconnectReason.MalformedPacket:
                case MqttClientDisconnectReason.ProtocolError:
                case MqttClientDisconnectReason.NotAuthorized:
                case MqttClientDisconnectReason.BadAuthenticationMethod:
                case MqttClientDisconnectReason.SessionTakenOver:
                case MqttClientDisconnectReason.TopicFilterInvalid:
                case MqttClientDisconnectReason.TopicNameInvalid:
                case MqttClientDisconnectReason.TopicAliasInvalid:
                case MqttClientDisconnectReason.PacketTooLarge:
                case MqttClientDisconnectReason.PayloadFormatInvalid:
                case MqttClientDisconnectReason.RetainNotSupported:
                case MqttClientDisconnectReason.QosNotSupported:
                case MqttClientDisconnectReason.ServerMoved:
                case MqttClientDisconnectReason.SharedSubscriptionsNotSupported:
                case MqttClientDisconnectReason.SubscriptionIdentifiersNotSupported:
                case MqttClientDisconnectReason.WildcardSubscriptionsNotSupported:
                    return true;
            }
            return false;
        }

        private static bool IsFatal(Exception e, bool userCancellationRequested = false)
        {
            switch (e)
            {
                case ConnectingFailedException mqttConnectingFailedException:
                    switch (mqttConnectingFailedException.ResultCode)
                    {
                        case MqttClientConnectResultCode.MalformedPacket:
                        case MqttClientConnectResultCode.ProtocolError:
                        case MqttClientConnectResultCode.UnsupportedProtocolVersion:
                        case MqttClientConnectResultCode.ClientIdentifierNotValid:
                        case MqttClientConnectResultCode.BadUserNameOrPassword:
                        case MqttClientConnectResultCode.Banned:
                        case MqttClientConnectResultCode.BadAuthenticationMethod:
                        case MqttClientConnectResultCode.TopicNameInvalid:
                        case MqttClientConnectResultCode.PacketTooLarge:
                        case MqttClientConnectResultCode.PayloadFormatInvalid:
                        case MqttClientConnectResultCode.RetainNotSupported:
                        case MqttClientConnectResultCode.QoSNotSupported:
                        case MqttClientConnectResultCode.ServerMoved:
                        case MqttClientConnectResultCode.ImplementationSpecificError:
                        case MqttClientConnectResultCode.UseAnotherServer:
                        case MqttClientConnectResultCode.NotAuthorized:
                            return true;
                    }
                    break;
                case SocketException:
                    // TODO there is room for a lot more nuance here. Some socket
                    // exceptions are more retryable than others so it may
                    // be inappropriate to label them all as fatal.
                    return true;
                case MqttProtocolViolationException:
                case ArgumentNullException:
                case ArgumentException:
                case NotSupportedException:
                    return true;
                //
                // MQTTnet may throw an OperationCanceledException even if
                // neither the user nor the session client provides a
                // cancellation token. Because of that, this exception is
                // only fatal if the cancellation token this layer
                // is aware of actually requested cancellation. Other cases
                // signify that MQTTnet gave up on the operation, but the
                // user still wants to retry.
                //
                case TaskCanceledException:
                case OperationCanceledException:
                    return userCancellationRequested;
            }
            return false;
        }

        /// <summary>
        /// Validate the size of the message before sending it to the MQTT broker.
        /// </summary>
        /// <param name="message">The message to validate.</param>
        /// <exception cref="InvalidOperationException">If the message size is too
        /// large.</exception>
        /// <remarks>
        /// </remarks>
        private Task ValidateMessageSize(MqttApplicationMessage message)
        {
            if (MaximumPacketSize > 0 && message.Payload.Length > MaximumPacketSize)
            {
                throw new InvalidOperationException(
                    $"Message size is too large. Maximum message size is {MaximumPacketSize} bytes.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle message receive
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            // Never let MQTTnet auto ack a message because it may do so out-of-order
            args.AutoAcknowledge = false;
            switch (args.ApplicationMessage.QualityOfServiceLevel)
            {
                case MqttQualityOfServiceLevel.AtMostOnce:
                    if (MessageReceived != null)
                    {
                        try
                        {
                            //
                            // QoS 0 messages don't have to worry about ack'ing or ack
                            // ordering, so just pass along the event args as-is.
                            //
                            await MessageReceived.Invoke(new MqttMessageReceivedEvent(
                                args.ClientId,
                                args.ApplicationMessage,
                                args.PacketIdentifier,
                                (args, ct) => Task.CompletedTask)).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Encountered an exception during the " +
                                "user-supplied callback for handling received messages.");
                        }
                    }

                    break;
                default:
                    var pendingAck = new PendingAck(args);

                    //
                    // Create a copy of the received message args, but with an
                    // acknowledge handler that simply signals that this message
                    // is ready to be acknowledged rather than actually sending
                    // the acknowledgement. This is done so that the
                    // acknowledgements can be sent in the order that the
                    // messages were received in instead of being sent at
                    // the moment the user acknowledges them.
                    //
                    var userFacingMessageReceivedEventArgs =
                        new MqttMessageReceivedEvent(
                            args.ClientId,
                            args.ApplicationMessage,
                            args.PacketIdentifier,
                            (args, ct) =>
                            {
                                pendingAck.MarkAsReady();
                                _pendingAcks.Signal();
                                return Task.CompletedTask;
                            });

                    _pendingAcks.Enqueue(pendingAck);

                    // By default, this client will automatically acknowledge a
                    // received message (in the right order)
                    userFacingMessageReceivedEventArgs.AutoAcknowledge = true;
                    if (MessageReceived != null)
                    {
                        try
                        {
                            //
                            // Note that this invocation does need to be awaited
                            // because the user is allowed/expected to set the
                            // AutoAcknowledge property on the provided args and
                            // the underlying MQTTnet client will auto acknowledge
                            // by default.
                            //
                            await MessageReceived.Invoke(userFacingMessageReceivedEventArgs)
                                .ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Encountered an exception during the " +
                                "user-supplied callback for handling received messages.");

                            // The user probably didn't get a chance to acknowledge the
                            // message, so send the acknowledgement for them.
                        }

                        //
                        // Even if the user wants to auto-acknowledge, we still need
                        // to go through the queue's ordering for each acknowledgement.
                        // Note that this means the underlying MQTT library will
                        // interpret all messages as AutoAcknowledge=false.
                        //
                        if (userFacingMessageReceivedEventArgs.AutoAcknowledge)
                        {
                            pendingAck.MarkAsReady();
                            _pendingAcks.Signal();
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Handle disconnection event from underlying client
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            lock (_ctsLockObj)
            {
                StopAcknowledgingReceivedMessages();
            }

            if (Disconnected != null)
            {
                _ = Disconnected.Invoke(args);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle connection event from underlying client
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
        {
            if (Connected != null)
            {
                _ = Connected.Invoke(args);
            }

            StartAcknowledgingReceivedMessages();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Publish all pending acks
        /// </summary>
        /// <param name="disconnected"></param>
        /// <returns></returns>
        private async Task PublishAcknowledgementsAsync(CancellationToken disconnected)
        {
            try
            {
                while (UnderlyingMqttClient.IsConnected)
                {
                    // This call will block until there is a first element in the queue and
                    // until that first element is ready to be acknowledged.
                    var queuedArgs = _pendingAcks.Dequeue(disconnected);
                    await queuedArgs.Args.AcknowledgeAsync(disconnected).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Send acknowledgements task cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Error while sending queued acknowledgements.");
            }
            finally
            {
                _logger.LogInformation("Stopped sending acknowledgements.");
            }
        }

        /// <summary>
        /// Start ack publishing
        /// </summary>
        private void StartAcknowledgingReceivedMessages()
        {
            if (!_disposed && _ackSenderTask == null)
            {
                _ackSenderTask = Task.Run(() => PublishAcknowledgementsAsync(
                    _ackSenderCts.Token),
                    _ackSenderCts.Token);
            }
        }

        /// <summary>
        /// Stop ack publishing
        /// </summary>
        private void StopAcknowledgingReceivedMessages()
        {
            _ackSenderCts.Cancel(false);
            _pendingAcks.Clear();

            _ackSenderCts?.Dispose();
            _ackSenderCts = new();
            _ackSenderTask = null;
        }

        /// <summary>
        /// A typed queued request and its associated metadata.
        /// </summary>
        internal sealed class Request<TOptions, TResult> : Request
        {
            public TOptions Options { get; }

            public TaskCompletionSource<TResult> ResultTaskCompletionSource { get; }

            /// <inheritdoc/>
            internal Request(TOptions options,
                TaskCompletionSource<TResult> resultTaskCompletionSource,
                CancellationToken ct) : base(ct)
            {
                Options = options;
                ResultTaskCompletionSource = resultTaskCompletionSource;
            }

            /// <inheritdoc/>
            public override void OnException(Exception reason)
            {
                ResultTaskCompletionSource.TrySetException(reason);
            }
        }

        /// <summary>
        /// A single enqueued publish, subscribe, or unsubscribe request and its
        /// associated metadata.
        /// </summary>
        internal abstract class Request
        {
            /// <summary>
            /// The cancellation token for this particular request.
            /// </summary>
            public CancellationToken CancellationToken { get; set; }

            /// <summary>
            /// If the request has been sent to the MQTT broker. This value will
            /// be reset if the connection is lost prior to this request being
            /// acknowledged.
            /// </summary>
            public bool IsInFlight { get; set; }

            /// <summary>
            /// Create queued request
            /// </summary>
            /// <param name="ct"></param>
            protected Request(CancellationToken ct)
            {
                CancellationToken = ct;
            }

            /// <summary>
            /// Create queued request
            /// </summary>
            /// <param name="request"></param>
            /// <param name="resultTaskCompletionSource"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public static Request Create<TOptions, TResult>(TOptions request,
                TaskCompletionSource<TResult> resultTaskCompletionSource,
                CancellationToken ct)
            {
                return new Request<TOptions, TResult>(request,
                    resultTaskCompletionSource, ct);
            }

            public abstract void OnException(Exception reason);
        }

        /// <summary>
        /// Request queue
        /// </summary>
        internal sealed class RequestQueue : IDisposable
        {
            public int Count => _requests.Count;

            /// <summary>
            /// Create queue
            /// </summary>
            /// <param name="maxSize"></param>
            /// <param name="overflowStrategy"></param>
            /// <exception cref="ArgumentException"></exception>
            public RequestQueue(uint maxSize, OverflowStrategy overflowStrategy)
            {
                if (maxSize < 1)
                {
                    throw new ArgumentException(
                        "Max queue size must be greater than 0");
                }
                _maxSize = maxSize;
                _overflowStrategy = overflowStrategy;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _gate.Dispose();
                _mutex.Dispose();
            }

            /// <summary>
            /// Add the item to the end of the list.
            /// </summary>
            /// <param name="item">The item to add.</param>
            /// <param name="ct">Cancellation token.</param>
            /// <remarks>
            /// If the item would overflow the list, then it will either
            /// push out the item at the front of the list or will itself
            /// be skipped.
            /// </remarks>
            public async Task AddLastAsync(Request item, CancellationToken ct)
            {
                ArgumentException.ThrowIfNullOrEmpty(nameof(item));

                await _mutex.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (_requests.Count < _maxSize)
                    {
                        _requests.AddLast(item);
                    }
                    else
                    {
                        if (_overflowStrategy == OverflowStrategy.DropNewMessage)
                        {
                            item.OnException(
                                new QueueOverflowException(_overflowStrategy));
                        }
                        else if (_requests.First != null &&
                            _overflowStrategy == OverflowStrategy.DropOldestQueuedMessage)
                        {
                            var first = _requests.First;
                            _requests.RemoveFirst();
                            first.Value.OnException(
                                new QueueOverflowException(_overflowStrategy));
                            _requests.AddLast(item);
                        }
                    }

                    _gate.Release();
                }
                finally
                {
                    _mutex.Release();
                }
            }

            /// <summary>
            /// Remove the provided item from the list.
            /// </summary>
            /// <param name="item">The item to remove from the list.
            /// </param>
            /// <param name="ct">Cancellation token.</param>
            /// <returns>True if the list contained the item. False
            /// otherwise.</returns>
            /// <remarks>
            /// Items can be removed from any position in the list.
            /// </remarks>
            public async Task<bool> RemoveAsync(Request item, CancellationToken ct)
            {
                await _mutex.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    return _requests.Remove(item);
                }
                finally
                {
                    _mutex.Release();
                }
            }

            /// <summary>
            /// Reset the state of each item in this list so that they
            /// can be sent again if necessary.
            /// </summary>
            /// <param name="ct">Cancellation token.</param>
            /// <remarks>
            /// This should be called whenever a connection is lost
            /// so that any sent-but-unacknowledged items can be sent again once
            /// the connection is recovered.
            /// </remarks>
            public async Task MarkMessagesAsUnsentAsync(CancellationToken ct)
            {
                await _mutex.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    foreach (var item in _requests)
                    {
                        if (!item.IsInFlight)
                        {
                            // Items in this list are ordered such that
                            // all unsent messages are at the back of the
                            // list, so the first encountered unsent
                            // message signals that the remaining messages
                            // also have not been sent, so they don't need
                            // to be looked at
                            break;
                        }
                        else
                        {
                            // This item was sent, but not acknowledged,
                            // so it should be sent again. Items that were
                            // sent and acknowledged should not be reset
                            // as they don't need to be sent again.
                            item.IsInFlight = false;
                            _gate.Release();
                        }
                    }
                }
                finally
                {
                    _mutex.Release();
                }
            }

            /// <summary>
            /// Peek the front-most item in the list that is not already
            /// in flight.
            /// </summary>
            /// <param name="ct"></param>
            /// <returns>The front-most item in the list that is not
            /// already in flight.</returns>
            public async Task<Request> PeekNextUnsentAsync(CancellationToken ct)
            {
                while (true)
                {
                    await _gate.WaitAsync(ct).ConfigureAwait(false);

                    var gateUsed = false;
                    try
                    {
                        await _mutex.WaitAsync(ct).ConfigureAwait(false);

                        try
                        {
                            var item = _requests.First;
                            if (item == null || _requests.Count == 0)
                            {
                                continue;
                            }

                            while (item.Value.IsInFlight)
                            {
                                item = item.Next;

                                if (item == null)
                                {
                                    break;
                                }
                            }

                            if (item != null)
                            {
                                gateUsed = true;
                                // The item should only be peeked when
                                // it is about to be sent
                                item.Value.IsInFlight = true;
                                return item.Value;
                            }
                        }
                        finally
                        {
                            _mutex.Release();
                        }
                    }
                    finally
                    {
                        if (!gateUsed)
                        {
                            _gate.Release(1);
                        }
                    }
                }
            }

            /// <summary>
            /// Cancel all
            /// </summary>
            /// <param name="reason"></param>
            /// <param name="ct"></param>
            /// <returns></returns>
            public async Task CancelAsync(Exception reason, CancellationToken ct)
            {
                try
                {
                    await _mutex.WaitAsync(ct).ConfigureAwait(false);
                    foreach (var item in _requests)
                    {
                        item.OnException(reason);
                    }

                    _requests.Clear();

                    // Clear the gate so that it reflects that there are
                    // no more queued requests
                    _gate.Dispose();
                    _gate = new SemaphoreSlim(0);
                }
                finally
                {
                    _mutex.Release();
                }
            }

            // This semaphore is responsible for allowing only one thread
            // to interact with this list at a time.
            private readonly SemaphoreSlim _mutex = new(1);
            // This semaphore is responsible for tracking how many items are
            // in the list for the purpose of blocking consumer threads
            // until there is at least one item to consume.
            private SemaphoreSlim _gate = new(0);
            private readonly LinkedList<Request> _requests = new();
            private readonly OverflowStrategy _overflowStrategy;
            private readonly uint _maxSize;
        }

        internal sealed class PendingAck
        {
            public MqttApplicationMessageReceivedEventArgs Args { get; }

            public PendingAck(MqttApplicationMessageReceivedEventArgs args)
            {
                Args = args;
            }

            public bool IsReady()
            {
                return _manuallyAcknowledged || Args.AutoAcknowledge;
            }

            public void MarkAsReady()
            {
                _manuallyAcknowledged = true;
            }

            private bool _manuallyAcknowledged;
        }

        /// <summary>
        /// A blocking queue that is thread safe and that allows for each
        /// element to specify when it is "ready" to be dequeued.
        /// </summary>
        /// <remarks>
        /// Items in this queue may be marked as ready to be dequeued in
        /// any order, but the blocking calls
        /// </remarks>
        private sealed class OrderedAckQueue : IDisposable
        {
            public int Count => _queue.Count;

            public OrderedAckQueue()
            {
                _queue = new ConcurrentQueue<PendingAck>();
                _gate = new ManualResetEventSlim(false);
            }

            /// <summary>
            /// Delete all entries from this queue.
            /// </summary>
            public void Clear()
            {
                _queue.Clear();
            }

            /// <summary>
            /// Enqueue the provided item.
            /// </summary>
            /// <param name="item">The item to enqueue.</param>
            public void Enqueue(PendingAck item)
            {
                _queue.Enqueue(item);
                _gate.Set();
            }

            /// <summary>
            /// Block until there is a first element in the queue and that
            /// element is ready to be dequeued then dequeue and
            /// return that element.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The first element in the queue.</returns>
            public PendingAck Dequeue(CancellationToken cancellationToken)
            {
                while (true)
                {
                    if (_queue.IsEmpty)
                    {
                        _gate.Wait(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }
                    else
                    {
                        if (_queue.TryPeek(out var item)
                            && item.IsReady()
                            && _queue.TryDequeue(out var dequeuedItem))
                        {
                            return dequeuedItem;
                        }
                        else
                        {
                            _gate.Reset();
                            _gate.Wait(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            continue;
                        }
                    }
                }
            }

            /// <summary>
            /// Wakeup any blocking calls not because a new element was added
            /// to the queue, but because one or more elements in the queue
            /// is now ready.
            /// </summary>
            /// <remarks>
            /// Generally, this method should be called every time an item in
            /// this queue is marked as ready.
            /// </remarks>
            public void Signal()
            {
                _gate.Set();
            }

            public void Dispose()
            {
                _gate.Dispose();
            }

            private readonly ConcurrentQueue<PendingAck> _queue;
            private readonly ManualResetEventSlim _gate;
        }

        /// <summary>
        /// Token refresh timer
        /// </summary>
        internal sealed class TokenRefreshTimer : IDisposable
        {
            /// <summary>
            /// Create token refresh timer
            /// </summary>
            /// <param name="outer"></param>
            /// <param name="tokenFilePath"></param>
            public TokenRefreshTimer(MqttSession outer, string tokenFilePath)
            {
                _tokenFilePath = tokenFilePath;
                var secondsToRefresh = GetTokenExpiry(File.ReadAllBytes(tokenFilePath));

                _refreshTimer = new Timer(RefreshToken, outer, secondsToRefresh * 1000,
                    Timeout.Infinite);
                outer._logger.LogInformation(
                    "Refresh token Timer set to {SecondsToRefresh} s.",
                    secondsToRefresh);
            }

            /// <summary>
            /// Timer callback
            /// </summary>
            /// <param name="state"></param>
            private void RefreshToken(object? state)
            {
                var outer = (MqttSession)state!;
                outer._logger.LogInformation("Refresh token Timer");
                if (outer.IsConnected)
                {
                    var token = File.ReadAllBytes(_tokenFilePath);
                    Task.Run(async () =>
                    {
                        await outer.SendEnhancedAuthenticationExchangeDataAsync(
                            new MqttEnhancedAuthenticationExchangeData
                            {
                                AuthenticationData = token,
                                ReasonCode = MqttAuthenticateReasonCode.ReAuthenticate
                            }, default).ConfigureAwait(false);
                    });
                    var secondsToRefresh = GetTokenExpiry(token);
                    _refreshTimer.Change(secondsToRefresh * 1000, Timeout.Infinite);
                    outer._logger.LogInformation(
                        "Refresh token Timer set to {SecondsToRefresh} s.",
                        secondsToRefresh);
                }
            }

            private static int GetTokenExpiry(byte[] token)
            {
                var jwtToken = new JwtSecurityTokenHandler()
                    .ReadJwtToken(Encoding.UTF8.GetString(token));
                return (int)jwtToken.ValidTo.Subtract(DateTime.UtcNow).TotalSeconds - 5;
            }

            public void Dispose()
            {
                _refreshTimer.Dispose();
                GC.SuppressFinalize(this);
            }

            private readonly Timer _refreshTimer = null!;
            private readonly string _tokenFilePath = null!;
        }

        /// <summary>
        /// Concrete message event arguments
        /// </summary>
        internal sealed class MqttMessageReceivedEvent : MqttMessageReceivedEventArgs
        {
            /// <summary>
            /// Acknoledgement handler
            /// </summary>
            /// <param name="args"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            internal delegate Task AckHandler(MqttMessageReceivedEventArgs args,
                CancellationToken cancellationToken);

            /// <inheritdoc/>
            public override MqttApplicationMessage ApplicationMessage { get; }

            /// <inheritdoc/>
            public override string ClientId { get; }

            /// <summary>
            /// Create arg
            /// </summary>
            /// <param name="clientId"></param>
            /// <param name="applicationMessage"></param>
            /// <param name="packetId"></param>
            /// <param name="acknowledgeHandler"></param>
            /// <exception cref="ArgumentNullException"></exception>
            internal MqttMessageReceivedEvent(string clientId,
                MqttApplicationMessage applicationMessage, ushort packetId,
                AckHandler acknowledgeHandler)
            {
                ClientId = clientId;
                ApplicationMessage = applicationMessage;
                PacketIdentifier = packetId;
                _acknowledgeHandler = acknowledgeHandler;
            }

            /// <inheritdoc/>
            public override Task AcknowledgeAsync(CancellationToken ct)
            {
                if (Interlocked.CompareExchange(ref _isAcknowledged, 1, 0) == 0)
                {
                    return _acknowledgeHandler(this, ct);
                }
                throw new InvalidOperationException(
                    "The application message is already acknowledged.");
            }

            private readonly AckHandler _acknowledgeHandler;
            private int _isAcknowledged;
        }

        private sealed record class Metrics : IDisposable
        {
            public Counter<long> Connect { get; }
            public Counter<long> Publish { get; }
            public Counter<long> Subscribe { get; }
            public Counter<long> Unsubscribe { get; }

            /// <summary>
            /// Create metrics
            /// </summary>
            /// <param name="meter"></param>
            /// <param name="outer"></param>
            public Metrics(Meter meter, MqttSession outer)
            {
                _meter = meter;

                Connect = meter.CreateCounter<long>("mqtt_connect",
                    description: "The number of connect results.");
                Publish = meter.CreateCounter<long>("mqtt_publish",
                    description: "The number of publish results.");
                Subscribe = meter.CreateCounter<long>("mqtt_subscribe",
                    description: "The number of publish results.");
                Unsubscribe = meter.CreateCounter<long>("mqtt_unsubscribe",
                    description: "The number of publish results.");

                meter.CreateObservableUpDownCounter<int>("mqtt_requests_pending",
                    () => new Measurement<int>(outer._pendingReqs.Count),
                    description: "The number of items in the session's request queue.");
                meter.CreateObservableUpDownCounter<int>("mqtt_acks_pending",
                    () => new Measurement<int>(outer._pendingAcks.Count),
                    description: "The number of items in the session's ack queue.");
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _meter.Dispose();
            }
            private readonly Meter _meter;
        }

        private Task? _ackSenderTask;
        private CancellationTokenSource _ackSenderCts = new();
        private CancellationTokenSource _workerCts = new();
        private CancellationTokenSource? _reconnectCts;
        private TokenRefreshTimer? _tokenRefresh;
        private readonly Metrics _metrics;
        private readonly RequestQueue _pendingReqs;
        private readonly IRetryPolicy _retryPolicy;
        private readonly OrderedAckQueue _pendingAcks = new();
        private readonly MqttOptions _options;
        private readonly Lock _ctsLockObj = new();
        private readonly ILogger _logger;
        private bool _disposed;
        private bool _isDesiredConnected;
        private bool _isClosing;
        private readonly SemaphoreSlim _disconnectedEventLock = new(1);
    }
}
