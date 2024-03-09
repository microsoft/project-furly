// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    /// <summary>
    /// Schema of an event
    /// </summary>
    public interface IEventSchema
    {
        /// <summary>
        /// Mime type
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Schema name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Version
        /// </summary>
        ulong Version { get; }

        /// <summary>
        /// Schema content
        /// </summary>
        string Schema { get; }

        /// <summary>
        /// Id
        /// </summary>
        string? Id { get; }
    }
}
