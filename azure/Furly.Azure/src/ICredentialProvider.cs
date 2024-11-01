// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure
{
    using global::Azure.Core;

    /// <summary>
    /// Provides credentials to use to autenticate to
    /// Azure services
    /// </summary>
    public interface ICredentialProvider
    {
        /// <summary>
        /// Credential provider
        /// </summary>
        TokenCredential Credential { get; }
    }
}
