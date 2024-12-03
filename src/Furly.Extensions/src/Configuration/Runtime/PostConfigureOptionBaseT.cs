// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Configuration
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using System;

    /// <summary>
    /// Post configuration base helper class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class PostConfigureOptionBase<T> : ConfigureOptionBase,
        IPostConfigureOptions<T> where T : class
    {
        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="configuration"></param>
        protected PostConfigureOptionBase(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public abstract void PostConfigure(string? name, T options);

        /// <summary>
        /// Helper to get options
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public IOptions<T> ToOptions()
        {
            var t = Configuration.Get<T?>() ?? Activator.CreateInstance<T>();
            if (t is null)
            {
                throw new InvalidOperationException(
                    $"Failed to create option of type {typeof(T)}");
            }
            PostConfigure(Options.DefaultName, t);
            return Options.Create(t);
        }
    }
}
