// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure.IoT.Edge;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage.Services;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Twin client implementation
    /// </summary>
    public sealed class IoTEdgeTwinClient : KVStoreCollection, IIoTEdgeTwinClient
    {
        /// <inheritdoc/>
        public IDictionary<string, VariantValue> Twin => this;

        /// <inheritdoc/>
        public override string Name => "IoTEdge";

        /// <summary>
        /// Create Twin client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        public IoTEdgeTwinClient(IIoTEdgeDeviceClient client, IJsonSerializer serializer,
            ILogger<IoTEdgeTwinClient> logger) : base(logger)
        {
            _client = client;
            _serializer = serializer;
            _logger = logger;

            StartStateSynchronization();
        }

        /// <inheritdoc/>
        public override async ValueTask<VariantValue?> TryPageInAsync(string key,
            CancellationToken ct)
        {
            try
            {
                var twin = await _client.GetTwinAsync(ct).ConfigureAwait(false);

                var property = twin.Properties.Desired[key];
                var value = (property?.Value == null) ? null :
                    (VariantValue)_serializer.FromObject(property.Value);

                ModifyState(state =>
                {
                    if (value == null)
                    {
                        state.Remove(key);
                    }
                    else
                    {
                        state.AddOrUpdate(key, value);
                    }
                });

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get twin...");
                return null;
            }
        }

        /// <inheritdoc/>
        protected override async ValueTask OnChangesAsync(IDictionary<string, VariantValue?> batch,
            CancellationToken ct)
        {
            var twin = new TwinCollection();
            foreach (var item in batch)
            {
                twin[item.Key] = item.Value.IsNull() ? null : item.Value!.ConvertTo<object>();
            }
            try
            {
                await _client.UpdateReportedPropertiesAsync(twin, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing values in batch {Batch}", twin);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnLoadState(CancellationToken ct)
        {
            // Register update handler
            await _client.SetDesiredPropertyUpdateCallbackAsync((desired, _) =>
            {
                ModifyState(state => OnUpdate(state, desired));
                return Task.CompletedTask;
            }, this, ct).ConfigureAwait(false);

            // Get twin
            _logger.LogDebug("Initialize device twin ...");
            for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
            {
                try
                {
                    var twin = await _client.GetTwinAsync(ct).ConfigureAwait(false);

                    ModifyState(state =>
                    {
                        // Start with reported values which we want applied
                        foreach (KeyValuePair<string, dynamic> property in twin.Properties.Reported)
                        {
                            var value = (VariantValue)_serializer.FromObject(property.Value);
                            if (!value.IsObject ||
                                !value.TryGetProperty("status", out _) ||
                                value.PropertyNames.Count() != 1)
                            {
                                if (!value.IsNull && !state.ContainsKey(property.Key))
                                {
                                    // Only add if not existing.
                                    state.Add(property.Key, value);
                                }
                            }
                        }

                        // Apply desired values on top.
                        OnUpdate(state, twin.Properties.Desired);
                        _logger.LogDebug("Device twin initialized successfully.");
                    });
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Attempt #{Attempt} failed in initializing twin - retrying...",
                        attempt);
                    await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
                }
            }

            void OnUpdate(IDictionary<string, VariantValue> state, TwinCollection desiredProperties)
            {
                foreach (KeyValuePair<string, dynamic> property in desiredProperties)
                {
                    if (property.Value == null)
                    {
                        state.Remove(property.Key);
                    }
                    else
                    {
                        var value = (VariantValue)_serializer.FromObject(property.Value);
                        state.AddOrUpdate(property.Key, value);
                    }
                }
            }
        }

        private readonly IIoTEdgeDeviceClient _client;
        private readonly IJsonSerializer _serializer;
        private readonly ILogger<IoTEdgeTwinClient> _logger;
    }
}
