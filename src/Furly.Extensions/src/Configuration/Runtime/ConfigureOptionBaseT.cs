// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Configuration
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Configuration base helper class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ConfigureOptionBase<T> : ConfigureOptionBase,
        IConfigureOptions<T>, IConfigureNamedOptions<T> where T : class
    {
        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="configuration"></param>
        protected ConfigureOptionBase(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public abstract void Configure(string? name, T options);

        /// <inheritdoc/>
        public void Configure(T options)
        {
            Configure(null, options);
        }
    }
}
