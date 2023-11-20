// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.RabbitMq
{
    /// <summary>
    /// Runtime environment variables
    /// </summary>
    public static class EnvironmentVariable
    {
        /// <summary> Rabbit mq host name </summary>
        public const string RABBITMQHOSTNAME =
            "RABBITMQ_HOSTNAME";
        /// <summary> Rabbit mq password </summary>
        public const string RABBITMQUSERNAME =
            "RABBITMQ_USERNAME";
        /// <summary> Rabbit mq secret </summary>
        public const string RABBITMQKEY =
            "RABBITMQ_KEY";
    }
}
