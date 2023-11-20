// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Assembly type extensions
    /// </summary>
    public static class AssemblyEx
    {
        /// <summary>
        /// Get assembly version
        /// </summary>
        /// <param name="assembly"></param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public static Version GetReleaseVersion(this Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            var ver = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (ver == null || !Version.TryParse(ver, out var assemblyVersion))
            {
                throw new KeyNotFoundException("Version attribute not found");
            }
            return assemblyVersion;
        }
    }
}
