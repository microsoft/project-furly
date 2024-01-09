// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router
{
    using System;

    /// <summary>
    /// Ignore method or property
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = true)]
    public sealed class IgnoreAttribute : Attribute;
}
