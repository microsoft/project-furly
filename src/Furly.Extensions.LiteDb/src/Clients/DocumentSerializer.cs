// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.LiteDb.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Json;
    using LiteDB;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// Provides document db and graph functionality for storage interfaces.
    /// </summary>
    internal static class DocumentSerializer
    {
        /// <summary>
        /// Mapper instance
        /// </summary>
        internal static BsonMapper Mapper { get; } = CreateMapper();

        /// <summary>
        /// Register type in mapper
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidProgramException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        internal static void Register<T>()
        {
            var dca = typeof(T).GetCustomAttribute<DataContractAttribute>();
            if (dca == null)
            {
                return; // Poco
            }
            // Map data contract object
            var builder = Mapper.Entity<T>();
            var field = typeof(EntityBuilder<T>).GetMethod("Field");
            if (field is null)
            {
                throw new InvalidProgramException("Missing Field method on entity builder");
            }
            var id = typeof(EntityBuilder<T>).GetMethod("Id");
            if (id is null)
            {
                throw new InvalidProgramException("Missing Id method on entity builder");
            }
            foreach (var prop in typeof(T).GetProperties())
            {
                var dma = prop.GetCustomAttribute<DataMemberAttribute>(true);
                if (dma == null)
                {
                    continue;
                }
                var paramex = Expression.Parameter(typeof(T));
                var expr = Expression.Lambda(Expression.Property(paramex, prop), paramex);
                if (dma.Name == "id")
                { // Cosmos db convention - use attribute going forward
                    // Create id accessor
                    builder = id.MakeGenericMethod(prop.PropertyType)
                        .Invoke(builder, [expr, /*assign auto id=*/ true])
                        as EntityBuilder<T>;
                }
                else
                {
                    // Create regular field property accessor
                    builder = field.MakeGenericMethod(prop.PropertyType)
                        .Invoke(builder, [expr, dma.Name ?? prop.Name])
                        as EntityBuilder<T>;
                }
                if (builder is null)
                {
                    throw new InvalidOperationException("Failed to configure entity builder.");
                }
            }
        }

        /// <summary>
        /// Create default mapper
        /// </summary>
        private static BsonMapper CreateMapper()
        {
            var mapper = new BsonMapper
            {
                // EnumAsInteger = true,
                // TrimWhitespace = false
            };

            var serializer = new DefaultJsonSerializer();

            // Override default time type handling
            mapper.RegisterType(
                ts => ts.HasValue ? ts.Value.Ticks :
                    BsonValue.Null,
                bs => bs.IsNull ? (TimeSpan?)null :
                    TimeSpan.FromTicks(bs.AsInt64));
            mapper.RegisterType(
                ts => ts.Ticks,
                bs => TimeSpan.FromTicks(bs.AsInt64));
            mapper.RegisterType(
                dt => dt.HasValue ? dt.Value.ToUnixTimeMilliseconds() :
                    BsonValue.Null,
                bs => bs.IsNull ? (DateTimeOffset?)null :
                    DateTimeOffset.FromUnixTimeMilliseconds(bs.AsInt64));
            mapper.RegisterType(
                dt => dt.ToUnixTimeMilliseconds(),
                bs => DateTimeOffset.FromUnixTimeMilliseconds(bs.AsInt64));
            mapper.RegisterType(
                dt => dt.HasValue ? dt.Value.ToBinary() :
                    BsonValue.Null,
                bs => bs.IsNull ? (DateTime?)null :
                    DateTime.FromBinary(bs.AsInt64));
            mapper.RegisterType(
                dt => dt.ToBinary(),
                bs => DateTime.FromBinary(bs.AsInt64));

            mapper.RegisterType(
                vv => vv.IsNull() ? BsonValue.Null :
                    serializer.SerializeToMemory(vv).ToArray(),
                bs => bs.IsNull ? VariantValue.Null :
                    serializer.Parse(bs.AsBinary.AsMemory()));

            mapper.RegisterType<IReadOnlyCollection<byte>>(
                b => b is byte[] binary ? binary : (b?.ToArray() ?? BsonValue.Null),
                bs => bs.AsBinary);

            return mapper;
        }
    }
}
