// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr
{
    /// <summary>
    /// Runtime environment variables
    /// </summary>
    public static class EnvironmentVariable
    {
        /// <summary> DAPR api token </summary>
        public const string DAPRAPITOKEN =
            "DAPR_API_TOKEN";
        /// <summary> DAPR http endpoint </summary>
        public const string DAPRHTTPENDPOINT =
            "DAPR_HTTP_ENDPOINT";
        /// <summary> DAPR grpc endpoint </summary>
        public const string DAPRGRPCENDPOINT =
            "DAPR_GRPC_ENDPOINT";
    }
}
