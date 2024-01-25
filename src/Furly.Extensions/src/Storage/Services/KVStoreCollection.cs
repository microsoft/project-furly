// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage.Services
{
    using Furly.Extensions.Serializers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    /// <summary>
    /// Key value store base class
    /// </summary>
    public abstract class KVStoreCollection : IKeyValueStore, IAwaitable<IKeyValueStore>,
        IDictionary<string, VariantValue>, IAsyncDisposable, IDisposable
    {
        /// <inheritdoc/>
        public IDictionary<string, VariantValue> State => this;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public ICollection<string> Keys
        {
            get
            {
                lock (_state)
                {
                    return _state.Keys.ToList();
                }
            }
        }

        /// <inheritdoc/>
        public ICollection<VariantValue> Values
        {
            get
            {
                lock (_state)
                {
                    return _state.Values.ToList();
                }
            }
        }

        /// <inheritdoc/>
        public int Count => _state.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public VariantValue this[string key]
        {
            get
            {
                lock (_state)
                {
                    return _state[key];
                }
            }
            set
            {
                lock (_state)
                {
                    if (_write.Writer.TryWrite((key, value)))
                    {
                        _state[key] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Create Twin client
        /// </summary>
        /// <param name="logger"></param>
        protected KVStoreCollection(ILogger<KVStoreCollection> logger)
        {
            _logger = logger;

            _write = Channel.CreateUnbounded<(string, VariantValue)>();
            _cts = new CancellationTokenSource();
        }

        /// <inheritdoc/>
        public abstract ValueTask<VariantValue?> TryPageInAsync(
            string key, CancellationToken ct);

        /// <inheritdoc/>
        public IAwaiter<IKeyValueStore> GetAwaiter()
        {
            return _loaded.AsAwaiter(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await DisposeAsync(true).ConfigureAwait(false);
            }
            finally
            {
                _cts.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void Add(string key, VariantValue value)
        {
            lock (_state)
            {
                if (_write.Writer.TryWrite((key, value)))
                {
                    _state.Add(key, value);
                }
            }
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            lock (_state)
            {
                return _state.ContainsKey(key);
            }
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            lock (_state)
            {
                lock (_state)
                {
                    if (_state.ContainsKey(key) &&
                        _write.Writer.TryWrite((key, VariantValue.Null)))
                    {
                        return _state.Remove(key);
                    }
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out VariantValue value)
        {
            lock (_state)
            {
                return _state.TryGetValue(key, out value);
            }
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, VariantValue> item)
        {
            Add(item.Key, item.Value);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_state)
            {
                foreach (var key in _state.Keys.ToList())
                {
                    if (_write.Writer.TryWrite((key, VariantValue.Null)))
                    {
                        _state.Remove(key);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, VariantValue> item)
        {
            lock (_state)
            {
                return _state.Contains(item);
            }
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, VariantValue>[] array, int arrayIndex)
        {
            lock (_state)
            {
                ((ICollection<KeyValuePair<string, VariantValue>>)_state).CopyTo(
                    array, arrayIndex);
            }
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, VariantValue> item)
        {
            lock (_state)
            {
                if (_state.TryGetValue(item.Key, out var removed) && item.Value == removed &&
                    _write.Writer.TryWrite((item.Key, VariantValue.Null)))
                {
                    return _state.Remove(item.Key);
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, VariantValue>> GetEnumerator()
        {
            return _state.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_state).GetEnumerator();
        }

        /// <summary>
        /// Synchronize the internal state
        /// </summary>
        /// <returns></returns>
        protected void StartStateSynchronization()
        {
            _loaded = OnLoadState(_cts.Token);
            _processor = _loaded.ContinueWith(_ =>
                Task.Factory.StartNew(() => SyncAsync(_cts.Token), _cts.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap(),
                    TaskScheduler.Default).Unwrap();

            async Task SyncAsync(CancellationToken ct)
            {
                try
                {
                    await OnLoadState(ct).ConfigureAwait(false);

                    // Now process reported changes to the state of the dictionary
                    while (!ct.IsCancellationRequested)
                    {
                        var batch = new Dictionary<string, VariantValue?>();
                        var item = await _write.Reader.ReadAsync(ct).ConfigureAwait(false);
                        do
                        {
                            batch.AddOrUpdate(item.Item1, item.Item2.IsNull ? null :
                                item.Item2);
                        }
                        while (_write.Reader.TryRead(out item));
                        await OnChangesAsync(batch, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process changes, existing.");
                }
                await OnExitAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Process changes to flush
        /// </summary>
        /// <param name="batch"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected abstract ValueTask OnChangesAsync(
            IDictionary<string, VariantValue?> batch, CancellationToken ct);

        /// <summary>
        /// Load initial state
        /// </summary>
        /// <param name="ct"></param>
        protected virtual Task OnLoadState(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle stoppage
        /// </summary>
        protected virtual ValueTask OnExitAsync()
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                DisposeAsync(true).AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                _cts.Dispose();
            }
        }

        /// <summary>
        /// Disposing
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing && _processor != null)
            {
                await _cts.CancelAsync().ConfigureAwait(false);
                try
                {
                    await _processor.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _processor = null;
                }
            }
        }

        /// <summary>
        /// Modify internal state
        /// </summary>
        /// <param name="processor"></param>
        protected void ModifyState(Action<IDictionary<string, VariantValue>> processor)
        {
            lock (_state)
            {
                processor(_state);
            }
        }

        private readonly ILogger<KVStoreCollection> _logger;
        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<string, VariantValue> _state = new();
        private readonly Channel<(string, VariantValue)> _write;
        private Task _loaded = Task.CompletedTask;
        private Task? _processor;
    }
}
