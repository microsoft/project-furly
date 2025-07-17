// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using System;

    /// <summary>
    /// Register event handler for AIO schema registry events.
    /// </summary>
    public interface IAioSrEvents
    {
        /// <summary>
        /// Register schema handlers
        /// </summary>
        IDisposable Register(IAioSrCallbacks callbacks);
    }
}
