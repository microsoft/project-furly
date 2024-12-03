// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Simple Command line options helper
    /// </summary>
    public sealed class CliOptions
    {
        /// <summary>
        /// Helper to collect options
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="offset">Offset into the array</param>
        public CliOptions(string[] args, int offset = 1)
        {
            _options = [];
            for (var i = offset; i < args.Length;)
            {
                var key = args[i];
                if (key[0] != '-')
                {
                    throw new ArgumentException($"{key} is not an option.");
                }
                i++;
                if (i == args.Length)
                {
                    _options.Add(key, "");
                    break;
                }
                var val = args[i];
                if (val[0] == '-')
                {
                    // An option, so previous one is a boolean option
                    _options.Add(key, "");
                    continue;
                }
                _options.Add(key, val);
                i++;
            }
        }

        /// <summary>
        /// Split command line
        /// </summary>
        /// <param name="commandLine"></param>
        public static string[] ParseAsCommandLine(string? commandLine)
        {
            char? quote = null;
            var isEscaping = false;
            if (commandLine == null)
            {
                return [];
            }
            return Split(commandLine, c =>
            {
                if (c == '\\' && !isEscaping)
                {
                    isEscaping = true;
                    return false;
                }
                if ((c == '"' || c == '\'') && !isEscaping)
                {
                    if (quote == c)
                    {
                        quote = null;
                    }
                    else
                    {
                        quote ??= c;
                    }
                }
                isEscaping = false;
                return quote == null && char.IsWhiteSpace(c);
            }, StringSplitOptions.RemoveEmptyEntries)
                .Select(arg => arg
                    .Trim()
                    .TrimMatchingChar(quote ?? ' ')
                    .Replace("\\\"", "\"", StringComparison.Ordinal))
                .Where(arg => !string.IsNullOrEmpty(arg))
                .ToArray();
        }

        /// <summary>
        /// Get option value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultValue"></param>
        /// <param name="keys"></param>
        public T GetValueOrDefault<T>(T defaultValue, params string[] keys)
        {
            if (!TryGetValue(keys, out var key, out var value))
            {
                return defaultValue;
            }
            return Get<T>(key, value);
        }

        /// <summary>
        /// Get option value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        public T? GetValueOrNull<T>(params string[] keys)
        {
            if (!TryGetValue(keys, out var key, out var value))
            {
                return default;
            }
            return Get<T>(key, value);
        }

        /// <summary>
        /// Get mandatory option value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        /// <exception cref="ArgumentException"></exception>
        public T GetValueOrThrow<T>(params string[] keys)
        {
            if (!TryGetValue(keys, out var key, out var value))
            {
                throw new ArgumentException(
                    $"Missing {string.Join('/', keys)} option.");
            }
            return Get<T>(key, value);
        }

        /// <summary>
        /// Get boolean option value
        /// </summary>
        /// <param name="keys"></param>
        public bool IsSet(params string[] keys)
        {
            if (!TryGetValue(keys, out var key, out var value))
            {
                return false;
            }
            return Is(key, value);
        }

        /// <summary>
        /// Get boolean option value or nullable
        /// </summary>
        /// <param name="keys"></param>
        public bool? IsProvidedOrNull(params string[] keys)
        {
            if (!TryGetValue(keys, out var key, out var value))
            {
                return null;
            }
            return Is(key, value);
        }

        /// <summary>
        /// Get the actual value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        private static T Get<T>(string key, string value)
        {
            if (typeof(T).IsEnum)
            {
                try
                {
                    return (T)Enum.Parse(typeof(T), value, true);
                }
                catch
                {
                    throw new ArgumentException("Value must be one of [" +
                        Enum.GetNames(typeof(T)).Aggregate((a, b) => a + ", " + b) + "]");
                }
            }
            try
            {
                return value.As<T>();
            }
            catch
            {
                throw new ArgumentException(
                    $"Invalid value '{value}' provided for parameter {key}.");
            }
        }

        /// <summary>
        /// Check whether value is true
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        private static bool Is(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }
            try
            {
                return value.As<bool>();
            }
            catch
            {
                throw new ArgumentException(
                    $"'{value}' cannot be evaluted as a boolean for parameter {key}.");
            }
        }

        /// <summary>
        /// Get value for keys
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        private bool TryGetValue(string[] keys,
            [NotNullWhen(true)] out string? key,
            [NotNullWhen(true)] out string? value)
        {
            if (keys.Length == 0)
            {
                throw new ArgumentException("Missing arguments.");
            }
            foreach (var k in keys)
            {
                if (_options.TryGetValue(k, out value))
                {
                    key = k;
                    return true;
                }
            }
            key = null;
            value = null;
            return false;
        }

        /// <summary>
        /// Split string using a predicate for each character that
        /// determines whether the position is a split point.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="predicate"></param>
        /// <param name="options"></param>
        private static IEnumerable<string> Split(string value, Func<char, bool> predicate,
            StringSplitOptions options = StringSplitOptions.None)
        {
            if (predicate == null)
            {
                yield return value;
            }
            else
            {
                var next = 0;
                for (var c = 0; c < value.Length; c++)
                {
                    if (predicate(value[c]))
                    {
                        var v = value[next..c];
                        if (options != StringSplitOptions.RemoveEmptyEntries ||
                            !string.IsNullOrEmpty(v))
                        {
                            yield return v;
                        }
                        next = c + 1;
                    }
                }
                if (options == StringSplitOptions.RemoveEmptyEntries && next == value.Length)
                {
                    yield break;
                }
                yield return value[next..];
            }
        }

        private readonly Dictionary<string, string> _options;
    }
}
