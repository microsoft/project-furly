// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System;

    /// <summary>
    /// Type extensions
    /// </summary>
    public static class TypeEx
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
    }
}
