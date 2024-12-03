// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    /// <summary>
    /// Edge extensions
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Return identity as string
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static string AsString(this IIoTEdgeDeviceIdentity identity)
        {
            return HubResource.Format(identity.Hub, identity.DeviceId,
                identity.ModuleId);
        }
    }
}
