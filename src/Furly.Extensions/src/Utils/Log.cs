// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Logging
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Log utils
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// Console logger
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="level"></param>
        public static ILogger<T> Console<T>(LogLevel? level = null)
        {
            using var factory = ConsoleFactory(level);
            return factory.CreateLogger<T>();
        }

        /// <summary>
        /// Console logger
        /// </summary>
        /// <param name="name"></param>
        /// <param name="level"></param>
        public static ILogger Console(string name, LogLevel? level = null)
        {
            using var factory = ConsoleFactory(level);
            return factory.CreateLogger(name);
        }

        /// <summary>
        /// Create logger factory
        /// </summary>
        /// <param name="level"></param>
        public static ILoggerFactory ConsoleFactory(LogLevel? level = null)
        {
            if (level == null)
            {
#if DEBUG
                level = LogLevel.Debug;
#else
                level = LogLevel.Information;
#endif
            }
            return LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                });
            });
        }
    }
}
