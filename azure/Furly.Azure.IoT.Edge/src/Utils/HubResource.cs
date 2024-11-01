// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    /// <summary>
    /// Hub resource utilities
    /// </summary>
    public static class HubResource
    {
        /// <summary>
        /// Parse hub resource
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="hub"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="errorMessage"></param>
        /// <param name="pathChar"></param>
        /// <returns></returns>
        public static bool Parse(string resource, out string? hub,
            [NotNullWhen(true)] out string? deviceId, out string? moduleId,
            [NotNullWhen(false)] out string? errorMessage, char pathChar = '_')
        {
            // Split path
            if (!kEncodings.TryGetValue(pathChar, out var replace))
            {
                throw new ArgumentException($"Unsupported parth character {pathChar}.");
            }
            var elements = resource.Split(pathChar, StringSplitOptions.RemoveEmptyEntries);
            var found = 0;
            hub = null;
            for (; found < elements.Length; found++)
            {
                if (elements[found].Equals(kDevicePrefix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                // hub comes pre-"devices"
                hub = GetString(pathChar, replace, elements, found);
            }
            if (++found >= elements.Length)
            {
                deviceId = null;
                moduleId = null;
                hub = null;
                errorMessage = "No deviceid found.";
                return false;
            }
            deviceId = GetString(pathChar, replace, elements, found);
            if (++found == elements.Length)
            {
                // Good but no module id
                moduleId = null;
                errorMessage = null;
                return true;
            }
            if (!elements[found].Equals(kModulePrefix,
                StringComparison.OrdinalIgnoreCase) ||
                ++found >= elements.Length)
            {
                errorMessage = "No moduleId found or more items than expected.";
                deviceId = null;
                moduleId = null;
                hub = null;
                return false;
            }

            moduleId = GetString(pathChar, replace, elements, found);
            if (++found != elements.Length)
            {
                errorMessage = "More items after moduleid than expected.";
                deviceId = null;
                moduleId = null;
                hub = null;
                return false;
            }

            errorMessage = null;
            return true;

            static string GetString(char pathChar, string replace, string[] elements,
                int found)
            {
                var str = elements[found];
                if (str.Contains(replace, StringComparison.Ordinal))
                {
                    str = str.Replace(replace, pathChar.ToString(),
                        StringComparison.Ordinal);
                }
                return str;
            }
        }

        /// <summary>
        /// Format hub resource
        /// </summary>
        /// <param name="hub"></param>
        /// <param name="deviceId"></param>
        /// <param name="moduleId"></param>
        /// <param name="pathChar"></param>
        /// <returns></returns>
        public static string Format(string? hub, string deviceId, string? moduleId,
            char pathChar = '_')
        {
            if (!kEncodings.TryGetValue(pathChar, out var replace))
            {
                throw new ArgumentException($"Unsupported parth character {pathChar}.");
            }
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(hub))
            {
                hub = GetString(hub, pathChar, replace);
                var index = hub.IndexOf('.', StringComparison.Ordinal);
                if (index != -1)
                {
                    hub = hub[..index];
                }
                sb.Append(hub);
                sb.Append(pathChar);
            }
            sb.Append(kDevicePrefix);
            sb.Append(pathChar);
            deviceId = GetString(deviceId, pathChar, replace);
            sb.Append(GetString(deviceId, pathChar, replace));
            if (!string.IsNullOrEmpty(moduleId))
            {
                sb.Append(pathChar);
                sb.Append(kModulePrefix);
                sb.Append(pathChar);
                sb.Append(GetString(moduleId, pathChar, replace));
            }
            return sb.ToString();

            static string GetString(string str, char pathChar, string replace)
            {
                if (str.Contains(pathChar, StringComparison.Ordinal))
                {
                    str = str.Replace(pathChar.ToString(),
                        replace, StringComparison.Ordinal);
                }
                return str;
            }
        }

        private const string kDevicePrefix = "device";
        private const string kModulePrefix = "module";
        // TODO: All special characters supported: - . % _ * ? ! ( ) , : = @ $ '
        private static readonly Dictionary<char, string> kEncodings = new()
        {
            ['_'] = "%5F",
            ['/'] = "%2F",
            ['.'] = "%2E",
        };
    }
}
