// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage.Services;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.StateStore;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store built on top of Distributed State Store
    /// </summary>
    public sealed class AioDssClient : KVStoreCollection
    {
        /// <inheritdoc/>
        public override string Name => "AioDss";

        /// <summary>
        /// Create aio state store
        /// </summary>
        /// <param name="client"></param>
        /// <param name="sdk"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public AioDssClient(IMqttPubSubClient client, IAioSdk sdk, ISerializer serializer,
            ILogger<AioDssClient> logger) : base(logger)
        {
            _logger = logger;
            _serializer = serializer;
            _dss = sdk.CreateStateStoreClient(client);
            _dss.KeyChangeMessageReceivedAsync += ClientKeyChangeMessageReceivedAsync;

            StartStateSynchronization();
        }

        /// <inheritdoc/>
        public override async ValueTask<VariantValue?> TryPageInAsync(
            string key, CancellationToken ct)
        {
            try
            {
                var response = await _dss.GetAsync(key, cancellationToken: ct)
                    .ConfigureAwait(false);
                // Subscribe to changes
                await _dss.ObserveAsync(key, cancellationToken: ct)
                    .ConfigureAwait(false);

                var state = _serializer.Deserialize<VariantValue>(response?.Value?.Bytes)
                    ?? VariantValue.Null;
                ModifyState(s => s.AddOrUpdate(key, state));
                return state;
            }
            catch (Exception ex)
            {
                _logger.FailedToPageIn(ex, key);
                return null;
            }
        }

        /// <inheritdoc/>
        protected override async ValueTask OnChangesAsync(
            IDictionary<string, VariantValue?> batch, CancellationToken ct)
        {
            // Process changes one by one
            foreach (var item in batch)
            {
                try
                {
                    if (item.Value == null)
                    {
                        // Unsubscribe to changes
                        await _dss.UnobserveAsync(item.Key, cancellationToken: ct)
                            .ConfigureAwait(false);
                        await _dss.DeleteAsync(item.Key,
                            cancellationToken: ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _dss.SetAsync(item.Key, new StateStoreValue(
                                _serializer.SerializeObjectToMemory(item.Value).ToArray()),
                            cancellationToken: ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.FailedToModifyState(ex,
                        item.Value == null ? "delete" : "save", item.Key);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task OnLoadState(CancellationToken ct)
        {
            try
            {
                await Task.Delay(0, ct).ConfigureAwait(false);
                //  var response = await _client.QueryStateAsync<VariantValue>(_store,
                //      "{}", cancellationToken: ct).ConfigureAwait(false);
                //
                //  ModifyState(state =>
                //  {
                //      foreach (var item in response.Results)
                //      {
                //          state.AddOrUpdate(item.Key, item.Data ?? VariantValue.Null);
                //      }
                //  });
            }
            catch (Exception ex)
            {
                _logger.FailedToLoadState(ex);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _dss.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Key changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        internal Task ClientKeyChangeMessageReceivedAsync(object? sender,
            KeyChangeMessageReceivedEventArgs arg)
        {
            ModifyState(state =>
            {
                var key = arg.ChangedKey.GetString();
                if (arg.NewState == KeyState.Deleted)
                {
                    state.Remove(key);
                }
                else
                {
                    state.AddOrUpdate(key,
                        _serializer.Deserialize<VariantValue>(arg.NewValue?.Bytes)
                        ?? VariantValue.Null);
                }
            });
            return Task.CompletedTask;
        }

        private readonly ISerializer _serializer;
        private readonly IStateStoreClient _dss;
        private readonly ILogger<AioDssClient> _logger;
    }

    /// <summary>
    /// Source-generated logging for AioDssClient
    /// </summary>
    internal static partial class AioDssClientLogging
    {
        private const int EventClass = 20;

        [LoggerMessage(EventId = EventClass + 0, Level = LogLevel.Debug,
            Message = "Failed to page in state for key {Key}")]
        public static partial void FailedToPageIn(this ILogger logger, Exception ex, string key);

        [LoggerMessage(EventId = EventClass + 1, Level = LogLevel.Error,
            Message = "Failed to {Action} state {Key}.")]
        public static partial void FailedToModifyState(this ILogger logger, Exception ex, string action, string key);

        [LoggerMessage(EventId = EventClass + 2, Level = LogLevel.Debug,
            Message = "Failed to load state using query. Query is optional api")]
        public static partial void FailedToLoadState(this ILogger logger, Exception ex);
    }
}
