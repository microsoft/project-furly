// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage.Services;
    using global::Dapr.Client;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store built on top of dapr
    /// </summary>
    public sealed class DaprStateStoreClient : KVStoreCollection
    {
        /// <inheritdoc/>
        public override string Name => "Dapr";

        /// <summary>
        /// Create dapr state store
        /// </summary>
        /// <param name="options"></param>
        /// <param name="provider"></param>
        /// <param name="logger"></param>
        public DaprStateStoreClient(IOptions<DaprOptions> options,
            IJsonSerializerSettingsProvider provider,
            ILogger<DaprStateStoreClient> logger) : base(logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            _store = string.IsNullOrEmpty(options.Value.StateStoreName)
                ? "default" : options.Value.StateStoreName;
            _client = options.Value.CreateClient(provider);
            _checkHealth = options.Value.CheckSideCarHealthBeforeAccess;
            _logger = logger;

            StartStateSynchronization();
        }

        /// <inheritdoc/>
        public override async ValueTask<VariantValue?> TryPageInAsync(
            string key, CancellationToken ct)
        {
            try
            {
                var state = await _client.GetStateAsync<VariantValue>(_store,
                    key, cancellationToken: ct).ConfigureAwait(false);

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
            if (_checkHealth)
            {
                await _client.WaitForSidecarAsync(ct).ConfigureAwait(false);
            }

            // Process changes one by one
            foreach (var item in batch)
            {
                try
                {
                    if (item.Value == null)
                    {
                        await _client.DeleteStateAsync(_store, item.Key,
                            cancellationToken: ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _client.SaveStateAsync(_store, item.Key,
                            item.Value, cancellationToken: ct).ConfigureAwait(false);
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
                if (_checkHealth)
                {
                    await _client.WaitForSidecarAsync(ct).ConfigureAwait(false);
                }

                var response = await _client.QueryStateAsync<VariantValue>(_store,
                    "{}", cancellationToken: ct).ConfigureAwait(false);

                ModifyState(state =>
                {
                    foreach (var item in response.Results)
                    {
                        state.AddOrUpdate(item.Key, item.Data ?? VariantValue.Null);
                    }
                });
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
                    _client.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private readonly string? _store;
        private readonly DaprClient _client;
        private readonly bool _checkHealth;
        private readonly ILogger<DaprStateStoreClient> _logger;
    }
}
