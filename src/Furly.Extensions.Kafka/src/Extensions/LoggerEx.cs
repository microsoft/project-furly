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
    internal static class LoggerEx
    {
        /// <summary>
        /// Handle log message
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="msg"></param>
        public static void Log(this ILogger logger, LogMessage msg)
        {
            LogLevel level;
            switch (msg.Level)
            {
                case SyslogLevel.Emergency:
                case SyslogLevel.Critical:
                case SyslogLevel.Warning:
                case SyslogLevel.Alert:
                    level = LogLevel.Warning;
                    break;
                case SyslogLevel.Error:
                    level = LogLevel.Error;
                    break;
                case SyslogLevel.Notice:
                case SyslogLevel.Info:
                    level = LogLevel.Information;
                    break;
                case SyslogLevel.Debug:
                    level = LogLevel.Debug;
                    break;
                default:
                    return;
            }
            logger.Log(level, "[{Facility}] {Name}: {Message}",
                msg.Facility, msg.Name, msg.Message);
        }
    }
}
