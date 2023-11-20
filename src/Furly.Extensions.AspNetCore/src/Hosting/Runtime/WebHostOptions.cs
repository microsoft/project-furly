// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Hosting
{
    /// <summary>
    /// Host configuration
    /// </summary>
    public class WebHostOptions
    {
        /// <summary>
        /// Whether to use https redirect and hsts
        /// </summary>
        public bool UseHttpsRedirect { get; set; }

        /// <summary>
        /// URL path base that service should be running on.
        /// </summary>
        public string? ServicePathBase { get; set; }
    }
}
