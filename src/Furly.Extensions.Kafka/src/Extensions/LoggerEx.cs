// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Confluent.Kafka
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Logger extensions
    /// </summary>
    public static partial class LoggerEx
    {
        [LoggerMessage(Message = "[{Facility}] {Name}: {Message}")]
        private static partial void WriteKafkaMessage(ILogger logger, LogLevel level,
            string facility, string name, string message);

        /// <summary>
        /// Handle Kafka message logging using mapped log levels.
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="msg">The Kafka message to log</param>
        public static void HandleKafkaMessage(this ILogger logger, LogMessage msg)
        {
            var level = msg.Level switch
            {
                SyslogLevel.Emergency or SyslogLevel.Critical or
                SyslogLevel.Warning or SyslogLevel.Alert => LogLevel.Warning,
                SyslogLevel.Error => LogLevel.Error,
                SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
                SyslogLevel.Debug => LogLevel.Debug,
                _ => LogLevel.None
            };
            if (level != LogLevel.None)
            {
                WriteKafkaMessage(logger, level, msg.Facility, msg.Name, msg.Message);
            }
        }
    }
}
