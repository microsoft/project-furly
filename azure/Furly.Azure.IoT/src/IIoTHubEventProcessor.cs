// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    using Furly.Extensions.Messaging;

    /// <summary>
    /// Event processor
    /// </summary>
    public interface IIoTHubEventProcessor :
        IEventRegistration<IIoTHubTelemetryHandler>;
}
