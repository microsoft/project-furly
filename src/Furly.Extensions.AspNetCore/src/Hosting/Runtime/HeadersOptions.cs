// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Hosting
{
    /// <summary>
    /// Forwarded headers processing configuration.
    /// </summary>
    public class HeadersOptions
    {
        /// <summary>
        /// Determines whethere processing of forwarded headers should be enabled or not.
        /// </summary>
        public bool ForwardingEnabled { get; set; }
    }
}
