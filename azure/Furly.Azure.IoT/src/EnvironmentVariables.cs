// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT
{
    /// <summary>
    /// Common runtime environment variables
    /// </summary>
    internal static class EnvironmentVariables
    {
        /// <summary> Iot hub connection string </summary>
        public const string PCS_IOTHUB_CONNSTRING =
            "PCS_IOTHUB_CONNSTRING";
        /// <summary> Iot hub event hub endpoint </summary>
        public const string PCS_IOTHUB_EVENTHUBENDPOINT =
            "PCS_IOTHUB_EVENTHUBENDPOINT";
        /// <summary> Event hub consumer group </summary>
        public const string PCS_IOTHUB_EVENTHUBCONSUMERGROUP =
            "PCS_IOTHUB_EVENTHUBCONSUMERGROUP";
        /// <summary> storage connection string </summary>
        public const string PCS_STORAGE_CONNSTRING =
            "PCS_STORAGE_CONNSTRING";
    }
}
