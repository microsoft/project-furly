// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Task extensions
    /// </summary>
    internal static class TaskEx
    {
        /// <summary>
        /// Timeout after some time
        /// </summary>
#pragma warning disable IDE1006 // Naming Styles
        public static async Task<T> WithTimeoutOf<T>(this Task<T> task,
            TimeSpan timeout, Func<T>? timeoutHandler = null)
#pragma warning restore IDE1006 // Naming Styles
        {
            var result = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (result != task)
            {
                return timeoutHandler != null ? timeoutHandler() : throw new TimeoutException($"Timeout after {timeout}");
            }
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Timeout after 2 minutes
        /// </summary>
        public static Task<T> With2MinuteTimeout<T>(this Task<T> task)
        {
            return task.WithTimeoutOf(TimeSpan.FromMinutes(2));
        }
    }
}
