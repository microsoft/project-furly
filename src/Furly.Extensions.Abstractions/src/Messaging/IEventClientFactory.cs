// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using System;

    /// <summary>
    /// Event client factory for creating event clients
    /// </summary>
    public interface IEventClientFactory
    {
        /// <summary>
        /// Name of the technology implementing the event client
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Create a new client using a connection string
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connectionString"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        IDisposable CreateEventClient(string context,
            string connectionString, out IEventClient client);
    }
}
