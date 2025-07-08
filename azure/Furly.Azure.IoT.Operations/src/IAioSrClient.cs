// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Messaging;

    /// <summary>
    /// Specialized interface to bind the aio schema registry to the publisher
    /// </summary>
    public interface IAioSrClient : ISchemaRegistry;
}
