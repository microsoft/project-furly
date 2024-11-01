// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure
{
    /// <summary>
    /// Azure credential options
    /// </summary>
    public record class CredentialOptions
    {
        /// <summary>
        /// Allow interactive login
        /// </summary>
        public bool? AllowInteractiveLogin { get; set; }
    }
}
