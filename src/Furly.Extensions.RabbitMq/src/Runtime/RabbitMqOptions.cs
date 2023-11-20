// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    /// <summary>
    /// RabbitMq configuration
    /// </summary>
    public class RabbitMqOptions
    {
        /// <summary>
        /// Host name
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// User name
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Secret
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Exchange name
        /// </summary>
        public string? Exchange { get; set; }

        /// <summary>
        /// The value configured in rabbit.max_message_size
        /// (Default: 512 MB)
        /// </summary>
        public int? MessageMaxBytes { get; set; }
    }
}
