// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure
{
    using Furly.Exceptions;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Retry helper class with different retry policies
    /// </summary>
    public static partial class ExponentialBackoff
    {
        /// <summary>Retry count max</summary>
        public static int DefaultMaxRetryCount { get; set; } = 10;

        /// <summary>
        /// Default exponential policy with 20% jitter
        /// </summary>
        public static Func<int, Exception, int> Policy => (k, ex) =>
            GetExponentialDelay(k, ExponentialBackoffIncrement, ExponentialMaxRetryCount);

        /// <summary>Max retry count for exponential policy</summary>
        public static int ExponentialMaxRetryCount { get; set; } = 13;
        /// <summary>Exponential backoff increment</summary>
        public static int ExponentialBackoffIncrement { get; set; } = 10;

        /// <summary>
        /// Helper to calcaulate exponential delay with jitter and max.
        /// </summary>
        /// <param name="k"></param>
        /// <param name="increment"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static int GetExponentialDelay(int k, int increment, int maxRetry)
        {
            if (k > maxRetry)
            {
                k = maxRetry;
            }
#pragma warning disable CA5394 // Do not use insecure randomness
            var backoff = Random.Shared.Next((int)(increment * 0.8), (int)(increment * 1.2));
#pragma warning restore CA5394 // Do not use insecure randomness
            var exp = 0.5 * (Math.Pow(2, k) - 1);
            var result = (int)(exp * backoff);
            System.Diagnostics.Debug.Assert(result > 0);
            return result;
        }

        /// <summary>
        /// Retries a piece of work
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="policy"></param>
        /// <param name="maxRetry"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException"></exception>
        internal static async Task DoAsync(ILogger logger, Func<Task> work, Func<Exception, bool> cont,
            Func<int, Exception, int> policy, int maxRetry, CancellationToken ct)
        {
            for (var k = 1; ; k++)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
                try
                {
                    await work().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    await DelayOrThrowAsync(logger, cont, policy, maxRetry, k, ex, ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retries a piece of work with return type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="policy"></param>
        /// <param name="maxRetry"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException"></exception>
        internal static async Task<T> DoAsync<T>(ILogger logger, Func<Task<T>> work, Func<Exception, bool> cont,
            Func<int, Exception, int> policy, int maxRetry, CancellationToken ct)
        {
            for (var k = 1; ; k++)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
                try
                {
                    return await work().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await DelayOrThrowAsync(logger, cont, policy, maxRetry, k, ex, ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retries a piece of work
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="policy"></param>
        /// <param name="maxRetry"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException"></exception>
        internal static async Task DoAsync(ILogger logger, Action work, Func<Exception, bool> cont,
            Func<int, Exception, int> policy, int maxRetry, CancellationToken ct)
        {
            for (var k = 1; ; k++)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
                try
                {
                    work();
                    return;
                }
                catch (Exception ex)
                {
                    await DelayOrThrowAsync(logger, cont, policy, maxRetry, k, ex, ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Func<Task> work,
            Func<Exception, bool> cont, CancellationToken ct, int? maxRetry = null)
        {
            return DoAsync(logger, work, cont, Policy, maxRetry ?? ExponentialMaxRetryCount, ct);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Func<Task> work,
            CancellationToken ct, int? maxRetry = null)
        {
            return RetryAsync(logger, work, ex => ex is ITransientException, ct, maxRetry);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Func<Task> work,
            int? maxRetry = null)
        {
            return RetryAsync(logger, work, default, maxRetry);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task<T> RetryAsync<T>(ILogger logger, Func<Task<T>> work,
            Func<Exception, bool> cont, CancellationToken ct, int? maxRetry = null)
        {
            return DoAsync(logger, work, cont, Policy, maxRetry ?? ExponentialMaxRetryCount, ct);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task<T> RetryAsync<T>(ILogger logger, Func<Task<T>> work,
            CancellationToken ct, int? maxRetry = null)
        {
            return RetryAsync(logger, work, (ex) => ex is ITransientException, ct, maxRetry);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task<T> RetryAsync<T>(ILogger logger, Func<Task<T>> work,
            int? maxRetry = null)
        {
            return RetryAsync(logger, work, default, maxRetry);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="cont"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Action work,
            Func<Exception, bool> cont, CancellationToken ct, int? maxRetry = null)
        {
            return DoAsync(logger, work, cont, Policy, maxRetry ?? ExponentialMaxRetryCount, ct);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="ct"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Action work,
            CancellationToken ct, int? maxRetry = null)
        {
            return RetryAsync(logger, work, ex => ex is ITransientException, ct, maxRetry);
        }

        /// <summary>
        /// Retry with exponential backoff
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        /// <param name="maxRetry"></param>
        /// <returns></returns>
        public static Task RetryAsync(ILogger logger, Action work, int? maxRetry = null)
        {
            return RetryAsync(logger, work, default, maxRetry);
        }

        /// <summary>
        /// Helper to run the delay policy and output additional information.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="cont"></param>
        /// <param name="policy"></param>
        /// <param name="maxRetry"></param>
        /// <param name="k"></param>
        /// <param name="ex"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private static async Task DelayOrThrowAsync(ILogger logger, Func<Exception, bool> cont,
            Func<int, Exception, int> policy, int maxRetry, int k, Exception ex,
            CancellationToken ct)
        {
            if (k > maxRetry || !cont(ex))
            {
                logger?.GiveUpAfterAttempt(ex, k);
                throw ex;
            }
            if (ex is TemporarilyBusyException tbx && tbx.RetryAfter != null)
            {
                var delay = tbx.RetryAfter.Value;
                Log(logger, k, (int)delay.TotalMilliseconds, ex);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            else
            {
                var delay = policy(k, ex);
                Log(logger, k, delay, ex);
                if (delay != 0)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Log
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="retry"></param>
        /// <param name="delay"></param>
        /// <param name="ex"></param>
        private static void Log(ILogger logger, int retry, int delay, Exception ex)
        {
            if (logger != null)
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.RetryWithDelayTrace(ex, retry, delay);
                }
                else
                {
                    logger.RetryWithDelayDebug(retry, delay);
                }
            }
        }

        [LoggerMessage(EventId = 0, Level = LogLevel.Trace,
          Message = "Give up after {Attempt}")]
        private static partial void GiveUpAfterAttempt(this ILogger logger,
            Exception ex, int attempt);

        [LoggerMessage(EventId = 1, Level = LogLevel.Trace,
            Message = "Retry {Attempt} in {Delay} ms...", SkipEnabledCheck = true)]
        private static partial void RetryWithDelayTrace(this ILogger logger,
            Exception ex, int attempt, int delay);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
            Message = "  ... Retry {Attempt} in {Delay} ms...")]
        private static partial void RetryWithDelayDebug(this ILogger logger,
            int attempt, int delay);
    }
}
