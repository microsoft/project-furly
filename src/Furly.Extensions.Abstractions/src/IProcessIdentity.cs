// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Hosting
{
    /// <summary>
    /// Process identity
    /// </summary>
    public interface IProcessIdentity
    {
        /// <summary>
        /// Process identity
        /// </summary>
        string Identity { get; }
    }
}
