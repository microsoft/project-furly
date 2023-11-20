// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Models
{
    using Furly.Extensions.Serializers;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Model of device registry / twin
    /// </summary>
    [DataContract]
    public sealed record class DeviceTwinModel
    {
        /// <summary>
        /// IoT Hub name
        /// </summary>
        [DataMember(Name = "hub",
            EmitDefaultValue = false)]
        public string? Hub { get; set; }

        /// <summary>
        /// Device id
        /// </summary>
        [DataMember(Name = "deviceId")]
        public string Id { get; set; } = null!;

        /// <summary>
        /// Module id
        /// </summary>
        [DataMember(Name = "moduleId",
            EmitDefaultValue = false)]
        public string? ModuleId { get; set; }

        /// <summary>
        /// Etag for comparison
        /// </summary>
        [DataMember(Name = "etag",
            EmitDefaultValue = false)]
        public string Etag { get; set; } = null!;

        /// <summary>
        /// Tags
        /// </summary>
        [DataMember(Name = "tags",
            EmitDefaultValue = false)]
        public IReadOnlyDictionary<string, VariantValue>? Tags { get; set; }

        /// <summary>
        /// Reported settings
        /// </summary>
        [DataMember(Name = "reported",
            EmitDefaultValue = false)]
        public IReadOnlyDictionary<string, VariantValue>? Reported { get; set; }

        /// <summary>
        /// Desired settings
        /// </summary>
        [DataMember(Name = "desired",
            EmitDefaultValue = false)]
        public IReadOnlyDictionary<string, VariantValue>? Desired { get; set; }

        /// <summary>
        /// Is iotedge device
        /// </summary>
        [DataMember(Name = "iotEdge",
            EmitDefaultValue = false)]
        public bool? IotEdge { get; set; }

        /// <summary>
        /// Twin's Version
        /// </summary>
        [DataMember(Name = "version",
            EmitDefaultValue = false)]
        public long? Version { get; set; }

        /// <summary>
        /// Gets the corresponding Device's Status.
        /// </summary>
        [DataMember(Name = "status",
            EmitDefaultValue = false)]
        public string? Status { get; set; }

        /// <summary>
        /// Reason, if any, for the corresponding Device
        /// to be in specified <see cref="Status"/>
        /// </summary>
        [DataMember(Name = "statusReason",
            EmitDefaultValue = false)]
        public string? StatusReason { get; set; }

        /// <summary>
        /// Time when the corresponding Device's
        /// <see cref="Status"/> was last updated
        /// </summary>
        [DataMember(Name = "statusUpdatedTime",
            EmitDefaultValue = false)]
        public DateTimeOffset? StatusUpdatedTime { get; set; }

        /// <summary>
        /// Corresponding Device's ConnectionState
        /// </summary>
        [DataMember(Name = "connectionState",
            EmitDefaultValue = false)]
        public string? ConnectionState { get; set; }

        /// <summary>
        /// Time when the corresponding Device was last active
        /// </summary>
        [DataMember(Name = "lastActivityTime",
            EmitDefaultValue = false)]
        public DateTimeOffset? LastActivityTime { get; set; }

        /// <summary>
        /// Primary sas key
        /// </summary>
        [DataMember(Name = "primaryKey",
            EmitDefaultValue = false)]
        public string? PrimaryKey { get; set; }

        /// <summary>
        /// Secondary sas key
        /// </summary>
        [DataMember(Name = "secondaryKey",
            EmitDefaultValue = false)]
        public string? SecondaryKey { get; set; }

        /// <summary>
        /// Device's Scope
        /// </summary>
        [DataMember(Name = "deviceScope",
            EmitDefaultValue = false)]
        public string? DeviceScope { get; set; }
    }
}
