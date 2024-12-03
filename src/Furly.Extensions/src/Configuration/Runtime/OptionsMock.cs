// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Configuration
{
    using Microsoft.Extensions.Options;
    using System;

    /// <summary>
    /// Options mock helper
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    public class OptionsMock<TOptions> : IOptions<TOptions>, IOptionsSnapshot<TOptions>,
        IOptionsMonitor<TOptions> where TOptions : class
    {
        /// <inheritdoc/>
        public OptionsMock(TOptions? options = null)
        {
            options ??= Activator.CreateInstance<TOptions>();
            if (options is null)
            {
                throw new InvalidOperationException(
                    $"Failed to create option of type {typeof(TOptions)}");
            }
            Value = options;
        }

        /// <inheritdoc/>
        public TOptions Value { get; }

        /// <inheritdoc/>
        public TOptions CurrentValue => Value;

        /// <inheritdoc/>
        public TOptions Get(string? name)
        {
            return Value;
        }

        /// <inheritdoc/>
        public IDisposable OnChange(Action<TOptions, string> listener)
        {
            return new Disposable();
        }

        private sealed class Disposable : IDisposable
        {
            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }
    }
}
