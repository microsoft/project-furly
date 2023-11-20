// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage.Services
{
    using Microsoft.Extensions.Options;
    using System.Threading.Tasks;

    /// <summary>
    /// Injectable collection factory
    /// </summary>
    public sealed class CollectionFactory : ICollectionFactory
    {
        /// <summary>
        /// Create container factory
        /// </summary>
        /// <param name="server"></param>
        /// <param name="options"></param>
        public CollectionFactory(IDatabaseServer server,
            IOptionsSnapshot<CollectionFactoryOptions> options)
        {
            _server = server;
            _options = options;
        }

        /// <inheritdoc/>
        public async Task<IDocumentCollection> OpenAsync(string? name)
        {
            var option = string.IsNullOrEmpty(name) ?
                _options.Value : _options.Get(name);
            var database = await _server.OpenAsync(
                option.DatabaseName ?? name).ConfigureAwait(false);
            return await database.OpenContainerAsync(
                option.ContainerName ?? name).ConfigureAwait(false);
        }

        private readonly IDatabaseServer _server;
        private readonly IOptionsSnapshot<CollectionFactoryOptions> _options;
    }
}
