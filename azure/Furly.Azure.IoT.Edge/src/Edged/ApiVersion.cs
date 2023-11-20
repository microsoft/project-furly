// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Api version
    /// </summary>
    internal sealed class ApiVersion : IComparable<ApiVersion>
    {
        public static readonly ApiVersion Version20180628 = new(1, "2018-06-28");
        public static readonly ApiVersion Version20190130 = new(2, "2019-01-30");
        public static readonly ApiVersion Version20191022 = new(3, "2019-10-22");
        public static readonly ApiVersion Version20191105 = new(4, "2019-11-05");
        public static readonly ApiVersion Version20200707 = new(5, "2020-07-07");
        public static readonly ApiVersion Version20211207 = new(6, "2021-12-07");
        public static readonly ApiVersion Version20220803 = new(7, "2022-08-03");
        public static readonly ApiVersion VersionUnknown = new(100, "Unknown");

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public int Value { get; }

        ApiVersion(int value, string name)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Parse version
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static ApiVersion ParseVersion(string str)
        {
            if (kInstance.TryGetValue(str, out var version))
            {
                return version;
            }
            return VersionUnknown;
        }

        /// <inheritdoc/>
        public int CompareTo(ApiVersion? other)
        {
            return Value.CompareTo(other?.Value);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is not ApiVersion version)
            {
                return false;
            }

            return version.Value == Value;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <inheritdoc/>
        public static bool operator ==(ApiVersion left, ApiVersion right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(ApiVersion left, ApiVersion right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public static bool operator <(ApiVersion left, ApiVersion right)
        {
            return left is null ? right is not null : left.CompareTo(right) < 0;
        }

        /// <inheritdoc/>
        public static bool operator <=(ApiVersion left, ApiVersion right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        /// <inheritdoc/>
        public static bool operator >(ApiVersion left, ApiVersion right)
        {
            return left?.CompareTo(right) > 0;
        }

        /// <inheritdoc/>
        public static bool operator >=(ApiVersion left, ApiVersion right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }

        /// <inheritdoc/>
        static readonly Dictionary<string, ApiVersion> kInstance = new()
        {
            { Version20180628.Name, Version20180628 },
            { Version20190130.Name, Version20190130 },
            { Version20191022.Name, Version20191022 },
            { Version20191105.Name, Version20191105 },
            { Version20200707.Name, Version20200707 },
            { Version20211207.Name, Version20211207 },
            { Version20220803.Name, Version20220803 }
        };
    }
}
