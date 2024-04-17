// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.EventHubs.Clients
{
    using Furly.Extensions.Hosting;
    using System;

    public class ProcessIdentityMock : IProcessIdentity
    {
        public string Identity { get; } = Guid.NewGuid().ToString();
    }
}
