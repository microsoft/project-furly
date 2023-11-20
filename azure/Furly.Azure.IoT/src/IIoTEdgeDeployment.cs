// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Extensions.Serializers;

    /// <summary>
    /// Layered deployment
    /// </summary>
    public interface IIoTEdgeDeployment
    {
        /// <summary>
        /// Identifier of the deployment
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Target condition
        /// </summary>
        string TargetCondition { get; }

        /// <summary>
        /// Name of the module
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Image name
        /// </summary>
        string Image { get; }

        /// <summary>
        /// Version
        /// </summary>
        string? Tag { get; }

        /// <summary>
        /// Create options
        /// </summary>
        VariantValue CreateOptions { get; }

        /// <summary>
        /// Priority
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Server url of the docker container registry
        /// to specify for this deployment
        /// </summary>
        string? DockerServer { get; }

        /// <summary>
        /// User
        /// </summary>
        string? DockerUser { get; }

        /// <summary>
        /// Password
        /// </summary>
        string? DockerPassword { get; }

        /// <summary>
        /// The deployment base layer identifier or null
        /// if no base layer should be created.
        /// </summary>
        string? BaseDeploymentId { get; }

        /// <summary>
        /// The base layer target condition. If null
        /// the target condition will be used.
        /// </summary>
        string? BaseTargetCondition { get; }
    }
}
