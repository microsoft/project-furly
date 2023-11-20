// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// Base object extensions
    /// </summary>
    public static class ObjectEx
    {
        /// <summary>
        /// Get generic interface
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericItfType"></param>
        /// <exception cref="ArgumentException"></exception>
        public static Type? GetCompatibleGenericInterface(this Type type,
            Type genericItfType)
        {
            if (!genericItfType.IsGenericType ||
                !genericItfType.IsInterface ||
                genericItfType != genericItfType.GetGenericTypeDefinition())
            {
                throw new ArgumentException(
                    "Argument must be a generic interface type" +
                    $" which {genericItfType.Name} is not.");
            }
            var check = type;
            if (check.IsGenericType)
            {
                check = check.GetGenericTypeDefinition();
            }
            if (check == genericItfType)
            {
                return type;
            }
            foreach (var itfOfType in type.GetInterfaces())
            {
                if (itfOfType.IsGenericType)
                {
                    var genericItf = itfOfType.GetGenericTypeDefinition();
                    if (genericItf == genericItfType)
                    {
                        return itfOfType;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Make nullable version
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="nil"></param>
        public static T? ToNullable<T>(this T value, T nil) where T : struct
        {
            return EqualityComparer<T>.Default.Equals(value, nil) ? null : value;
        }

        /// <summary>
        /// Safe equals
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="that"></param>
        public static bool EqualsSafe(this object? obj, object? that)
        {
            if (obj == that)
            {
                return true;
            }
            if (obj == null || that == null)
            {
                return false;
            }
            return obj.Equals(that);
        }

        /// <summary>
        /// Using type converter, convert type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static T As<T>(this object value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var converted = (T?)As(value, typeof(T));
            if (converted is not null)
            {
                return converted;
            }
            throw new NotSupportedException(
                $"Cannot convert value {value} to type {typeof(T)}.");
        }

        /// <summary>
        /// Using type converter, convert type
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        public static object? As(this object? value, Type type)
        {
            if (value == null || value.GetType() == type)
            {
                return value;
            }
            var converter = TypeDescriptor.GetConverter(type);
            return converter.ConvertFrom(value);
        }
    }
}
