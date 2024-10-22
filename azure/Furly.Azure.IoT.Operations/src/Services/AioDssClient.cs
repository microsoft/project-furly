// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage.Services;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.LeasedLock;
    using global::Azure.Iot.Operations.Services.StateStore;
    using k8s.LeaderElection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store built on top of dapr
    /// </summary>
    public sealed class AioDssClient : KVStoreCollection
    {
        /// <inheritdoc/>
        public override string Name => "Aio";

        /// <summary>
        /// Create aio state store
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public AioDssClient(IMqttPubSubClient client, ISerializer serializer,
            ILogger<AioDssClient> logger) : base(logger)
        {
            _logger = logger;
            _serializer = serializer;
            _dss = new StateStoreClient(client);
            _dss.KeyChangeMessageReceivedAsync += _client_KeyChangeMessageReceivedAsync;

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
                _logger.LogDebug(ex, "Failed to page in state for key {Key}", key);
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
                    _logger.LogError(ex, "Failed to {Action} state {Key}.",
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
                _logger.LogDebug(ex,
                    "Failed to load state using query. Query is optional api");
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
        /// <exception cref="NotImplementedException"></exception>
        private Task _client_KeyChangeMessageReceivedAsync(object? sender,
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
        private readonly StateStoreClient _dss;
        private readonly ILogger<AioDssClient> _logger;
    }
}
