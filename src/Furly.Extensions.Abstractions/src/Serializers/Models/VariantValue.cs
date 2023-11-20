// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using System.Text;

    /// <summary>
    /// Represents primitive or structurally complex value
    /// </summary>
    [DebuggerDisplay($"Raw = {{{nameof(RawValueAsString)}}}")]
    public abstract class VariantValue : ICloneable, IConvertible, IComparable
    {
        /// <summary>
        /// Null constant
        /// </summary>
        public static readonly VariantValue Null =
            new PrimitiveValue(null, VariantValueType.Null);

        /// <summary>
        /// Test for null
        /// </summary>
        /// <param name="value"></param>
        public static bool IsNullOrNullValue([NotNullWhen(false)] VariantValue? value)
        {
            return value?.IsNull != false;
        }

        /// <inheritdoc/>
        public VariantValue this[string key]
        {
            get
            {
                if (!TryGetProperty(key, out var result))
                {
                    result = AddProperty(key);
                }
                return result;
            }
        }

        /// <inheritdoc/>
        public VariantValue this[int index]
        {
            get
            {
                if (!TryGetElement(index, out var result))
                {
                    // Fall back to use array elements
                    result = Values.Skip(index).FirstOrDefault();
                    if (result is null)
                    {
                        return Null;
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Property names of object
        /// </summary>
        public IEnumerable<string> PropertyNames => GetObjectProperties();

        /// <summary>
        /// The primitive value
        /// </summary>
        public object? Value
        {
            get
            {
                if (!TryGetValue(out var v, CultureInfo.InvariantCulture))
                {
                    return null;
                }
                return v;
            }
        }

        /// <summary>
        /// Array elements
        /// </summary>
        public IEnumerable<VariantValue> Values
        {
            get
            {
                if (TryGetBytes(out var bytes, true, CultureInfo.InvariantCulture))
                {
                    return bytes.Select(b => new PrimitiveValue(b));
                }
                return GetArrayElements();
            }
        }

        /// <summary>
        /// Length of array
        /// </summary>
        public int Count
        {
            get
            {
                if (TryGetBytes(out var bytes, true, CultureInfo.InvariantCulture))
                {
                    return bytes.Length;
                }
                return GetArrayCount();
            }
        }

        /// <summary>
        /// Value is a list
        /// </summary>
        public bool IsListOfValues =>
            GetValueType() == VariantValueType.Values;

        /// <summary>
        /// Value is a array - includes bytes
        /// </summary>
        public bool IsArray =>
            GetValueType() == VariantValueType.Values || IsBytes;

        /// <summary>
        /// Value is a object type
        /// </summary>
        public bool IsObject =>
            GetValueType() == VariantValueType.Complex;

        /// <summary>
        /// Value is a null type
        /// </summary>
        public bool IsNull =>
            GetValueType() == VariantValueType.Null;

        /// <summary>
        /// Value is a decimal type
        /// </summary>
        public bool IsDecimal =>
            TryGetDecimal(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a integer type
        /// </summary>
        public bool IsInteger =>
            TryGetBigInteger(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a int64 type
        /// </summary>
        public bool IsInt64 =>
            TryGetInt64(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a uint64 type
        /// </summary>
        public bool IsUInt64 =>
            TryGetInt64(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a double type
        /// </summary>
        public bool IsDouble =>
            TryGetDouble(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a float type
        /// </summary>
        public bool IsFloat =>
            TryGetSingle(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a duration type
        /// </summary>
        public bool IsTimeSpan =>
            TryGetTimeSpan(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a date type
        /// </summary>
        public bool IsDateTime =>
            TryGetDateTime(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a Guid type
        /// </summary>
        public bool IsGuid =>
            TryGetGuid(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a boolean type
        /// </summary>
        public bool IsBoolean =>
            TryGetBoolean(out _, false, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a string type
        /// </summary>
        public bool IsString =>
            TryGetString(out _, true, CultureInfo.InvariantCulture);

        /// <summary>
        /// Value is a bytes type
        /// </summary>
        public bool IsBytes =>
            TryGetBytes(out _, false, CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public virtual TypeCode GetTypeCode()
        {
            if (IsNullOrNullValue(this))
            {
                return TypeCode.Empty;
            }
            if (TryGetBoolean(out _, true, CultureInfo.InvariantCulture))
            {
                return TypeCode.Boolean;
            }
            if (TryGetSingle(out _, true, CultureInfo.InvariantCulture))
            {
                return TypeCode.Single;
            }
            if (TryGetDouble(out _, true, CultureInfo.InvariantCulture))
            {
                return TypeCode.Double;
            }
            if (TryGetBytes(out _, true, CultureInfo.InvariantCulture))
            {
                return TypeCode.String;
            }
            if (TryGetDecimal(out _, false, CultureInfo.InvariantCulture))
            {
                return TypeCode.Decimal;
            }
            if (IsObject)
            {
                return TypeCode.Object;
            }
            return TypeCode.String;
        }

        /// <inheritdoc/>
        public bool ToBoolean(IFormatProvider? provider)
        {
            if (TryGetBoolean(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<bool>(default);
        }

        /// <inheritdoc/>
        public static explicit operator bool(VariantValue value) =>
            !IsNullOrNullValue(value) &&
                value.ToBoolean(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator bool?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value!.ToBoolean(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(bool value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(bool? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public byte ToByte(IFormatProvider? provider)
        {
            if (TryGetUInt64(out var value, false, provider))
            {
                return (byte)value;
            }
            return ConvertTo<byte>(default);
        }
        /// <inheritdoc/>
        public static explicit operator byte(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToByte(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator byte?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value!.ToByte(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(byte value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(byte? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public char ToChar(IFormatProvider? provider)
        {
            if (TryGetChar(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<char>(default);
        }
        /// <inheritdoc/>
        public static explicit operator char(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToChar(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator char?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value!.ToChar(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(char value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(char? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public DateTime ToDateTime(IFormatProvider? provider)
        {
            if (TryGetDateTime(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<DateTime>(default);
        }
        /// <inheritdoc/>
        public static explicit operator DateTime(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToDateTime(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator DateTime?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToDateTime(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(DateTime value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(DateTime? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public DateTimeOffset ToDateTimeOffset(IFormatProvider? provider)
        {
            if (TryGetDateTimeOffset(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<DateTimeOffset>(default);
        }
        /// <inheritdoc/>
        public static explicit operator DateTimeOffset(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToDateTimeOffset(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator DateTimeOffset?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToDateTimeOffset(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(DateTimeOffset value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(DateTimeOffset? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public decimal ToDecimal(IFormatProvider? provider)
        {
            if (TryGetDecimal(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<decimal>(default);
        }
        /// <inheritdoc/>
        public static explicit operator decimal(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToDecimal(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator decimal?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToDecimal(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(decimal value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(decimal? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public double ToDouble(IFormatProvider? provider)
        {
            if (TryGetDouble(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<double>(default);
        }
        /// <inheritdoc/>
        public static explicit operator double(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToDouble(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator double?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToDouble(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(double value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(double? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public short ToInt16(IFormatProvider? provider)
        {
            if (TryGetInt64(out var value, false, provider))
            {
                return (short)value;
            }
            return ConvertTo<short>(default);
        }
        /// <inheritdoc/>
        public static explicit operator short(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToInt16(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator short?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToInt16(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(short value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(short? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public int ToInt32(IFormatProvider? provider)
        {
            if (TryGetInt64(out var value, false, provider))
            {
                return (int)value;
            }
            return ConvertTo<int>(default);
        }
        /// <inheritdoc/>
        public static explicit operator int(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToInt32(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator int?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToInt32(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(int value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(int? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public long ToInt64(IFormatProvider? provider)
        {
            if (TryGetInt64(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<long>(default);
        }
        /// <inheritdoc/>
        public static explicit operator long(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToInt64(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator long?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToInt64(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(long value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(long? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public ushort ToUInt16(IFormatProvider? provider)
        {
            if (TryGetUInt64(out var value, false, provider))
            {
                return (ushort)value;
            }
            return ConvertTo<ushort>(default);
        }
        /// <inheritdoc/>
        public static explicit operator ushort(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToUInt16(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator ushort?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToUInt16(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(ushort value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(ushort? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public uint ToUInt32(IFormatProvider? provider)
        {
            if (TryGetUInt64(out var value, false, provider))
            {
                return (uint)value;
            }
            return ConvertTo<uint>(default);
        }
        /// <inheritdoc/>
        public static explicit operator uint(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToUInt32(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator uint?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToUInt32(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(uint value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(uint? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public ulong ToUInt64(IFormatProvider? provider)
        {
            if (TryGetUInt64(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<ulong>(default);
        }
        /// <inheritdoc/>
        public static explicit operator ulong(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToUInt64(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator ulong?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToUInt64(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(ulong value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(ulong? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public sbyte ToSByte(IFormatProvider? provider)
        {
            if (TryGetInt64(out var value, false, provider))
            {
                return (sbyte)value;
            }
            return ConvertTo<sbyte>(default);
        }
        /// <inheritdoc/>
        public static explicit operator sbyte(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToSByte(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator sbyte?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToSByte(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(sbyte value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(sbyte? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public float ToSingle(IFormatProvider? provider)
        {
            if (TryGetSingle(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<float>(default);
        }
        /// <inheritdoc/>
        public static explicit operator float(VariantValue value) =>
            IsNullOrNullValue(value) ? throw new ArgumentNullException(nameof(value)) :
                value.ToSingle(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator float?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToSingle(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(float value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(float? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public string ToString(IFormatProvider? provider)
        {
            if (TryGetString(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo(string.Empty);
        }
        /// <inheritdoc/>
        public static explicit operator string?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToString(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(string? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public byte[] ToBytes(IFormatProvider? provider)
        {
            if (TryGetBytes(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo(Array.Empty<byte>());
        }
        /// <inheritdoc/>
        public static explicit operator byte[]?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToBytes(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(byte[]? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public Guid ToGuid(IFormatProvider? provider)
        {
            if (TryGetGuid(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<Guid>(default);
        }
        /// <inheritdoc/>
        public static explicit operator Guid(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToGuid(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator Guid?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToGuid(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(Guid value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(Guid? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public TimeSpan ToTimeSpan(IFormatProvider? provider)
        {
            if (TryGetTimeSpan(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<TimeSpan>(default);
        }
        /// <inheritdoc/>
        public static explicit operator TimeSpan(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToTimeSpan(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator TimeSpan?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToTimeSpan(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(TimeSpan value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(TimeSpan? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public BigInteger ToBigInteger(IFormatProvider? provider)
        {
            if (TryGetBigInteger(out var value, false, provider))
            {
                return value;
            }
            return ConvertTo<BigInteger>(default);
        }
        /// <inheritdoc/>
        public static explicit operator BigInteger(VariantValue value) =>
            IsNullOrNullValue(value) ? default :
                value.ToBigInteger(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static explicit operator BigInteger?(VariantValue? value) =>
            IsNullOrNullValue(value) ? null :
                value.ToBigInteger(CultureInfo.InvariantCulture);
        /// <inheritdoc/>
        public static implicit operator VariantValue(BigInteger value) =>
            new PrimitiveValue(value);
        /// <inheritdoc/>
        public static implicit operator VariantValue(BigInteger? value) =>
            new PrimitiveValue(value);

        /// <inheritdoc/>
        public virtual object ToType(Type conversionType, IFormatProvider? provider)
        {
            ArgumentNullException.ThrowIfNull(conversionType);
            var rawValue = GetRawValue();
            if (rawValue is null || IsNull)
            {
                var created = Activator.CreateInstance(conversionType);
                if (created is not null)
                {
                    return created;
                }
                throw new InvalidOperationException(
                    $"Failed to create object of type {conversionType.Name}");
            }
            if (conversionType.IsAssignableFrom(rawValue.GetType()))
            {
                return rawValue;
            }
            if (rawValue is IConvertible c)
            {
                return c.ToType(conversionType,
                    provider ?? CultureInfo.InvariantCulture);
            }
            if (conversionType == typeof(byte))
            {
                return ToByte(provider);
            }
            if (conversionType == typeof(float))
            {
                return ToSingle(provider);
            }
            if (conversionType == typeof(double))
            {
                return ToDouble(provider);
            }
            if (conversionType == typeof(sbyte))
            {
                return ToSByte(provider);
            }
            if (conversionType == typeof(short))
            {
                return ToInt16(provider);
            }
            if (conversionType == typeof(int))
            {
                return ToInt32(provider);
            }
            if (conversionType == typeof(long))
            {
                return ToInt64(provider);
            }
            if (conversionType == typeof(ushort))
            {
                return ToUInt16(provider);
            }
            if (conversionType == typeof(uint))
            {
                return ToUInt32(provider);
            }
            if (conversionType == typeof(ulong))
            {
                return ToUInt64(provider);
            }
            if (conversionType == typeof(char))
            {
                return ToChar(provider);
            }
            if (conversionType == typeof(string))
            {
                return ToString(provider);
            }
            if (conversionType == typeof(Guid))
            {
                return ToGuid(provider);
            }
            if (conversionType == typeof(bool))
            {
                return ToBoolean(provider);
            }
            if (conversionType == typeof(byte[]))
            {
                return ToBytes(provider);
            }
            if (conversionType == typeof(decimal))
            {
                return ToDecimal(provider);
            }
            if (conversionType == typeof(DateTimeOffset))
            {
                return ToDateTimeOffset(provider);
            }
            if (conversionType == typeof(TimeSpan))
            {
                return ToTimeSpan(provider);
            }
            if (conversionType == typeof(DateTime))
            {
                return ToDateTime(provider);
            }
            throw new InvalidOperationException(
                $"Failed to convert object to type {conversionType.Name}");
        }

        /// <inheritdoc/>
        public static bool operator ==(VariantValue? left, VariantValue? right) =>
            Equality.Equals(left, right);
        /// <inheritdoc/>
        public static bool operator !=(VariantValue? left, VariantValue? right) =>
            !Equality.Equals(left, right);
        /// <inheritdoc/>
        public static bool operator >(VariantValue? left, VariantValue? right) =>
            Comparer.Compare(left, right) > 0;
        /// <inheritdoc/>
        public static bool operator <(VariantValue? left, VariantValue? right) =>
            Comparer.Compare(left, right) < 0;
        /// <inheritdoc/>
        public static bool operator >=(VariantValue? left, VariantValue? right) =>
            Comparer.Compare(left, right) >= 0;
        /// <inheritdoc/>
        public static bool operator <=(VariantValue? left, VariantValue? right) =>
            Comparer.Compare(left, right) <= 0;

        /// <summary>
        /// Equality helper for easier porting
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public static bool DeepEquals(VariantValue? x, VariantValue? y)
        {
            return Equality.Equals(x, y);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is VariantValue v)
            {
                return Equality.Equals(this, v);
            }

            return VariantValueComparer.EqualValues(this, obj);
        }

        /// <inheritdoc/>
        public int CompareTo(object? obj)
        {
            if (obj is VariantValue v)
            {
                return Comparer.Compare(this, v);
            }
            return VariantValueComparer.CompareValues(this, obj);
        }

        /// <inheritdoc/>
        public string RawValueAsString
        {
            get
            {
                var raw = GetRawValue();
                if (raw != null)
                {
                    // Append raw value as string
                    if (raw is IFormattable f and not Guid)
                    {
                        return f.ToString("G", CultureInfo.InvariantCulture);
                    }
                    var s = raw.ToString();
                    if (s != null)
                    {
                        return s;
                    }
                }
                return "null";
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // Append stringified value
            var sb = new StringBuilder()
                .Append("{ ")
                .Append("\"Value\": ");

            sb = AppendTo(sb)
                .Append(", ")
                .Append("\"Raw\": \"")
                .Append(RawValueAsString) // Append raw value as string
                .Append('"');

            // Append tests to inspect value recognition
            AppendProperty(sb, nameof(IsObject), IsObject);
            AppendProperty(sb, nameof(IsArray), IsArray);

            if (GetValueType() == VariantValueType.Values)
            {
                // Append raw value as string
                sb = sb.Append(", ")
                    .Append("\"Count\": ")
                    .Append(Count);
            }

            AppendProperty(sb, nameof(IsBoolean), IsBoolean);
            AppendProperty(sb, nameof(IsString), IsString);
            AppendProperty(sb, nameof(IsBytes), IsBytes);
            AppendProperty(sb, nameof(IsDecimal), IsDecimal);
            AppendProperty(sb, nameof(IsDouble), IsDouble);
            AppendProperty(sb, nameof(IsFloat), IsFloat);
            AppendProperty(sb, nameof(IsInt64), IsInt64);
            AppendProperty(sb, nameof(IsUInt64), IsUInt64);
            AppendProperty(sb, nameof(IsInteger), IsInteger);
            AppendProperty(sb, nameof(IsGuid), IsGuid);
            AppendProperty(sb, nameof(IsDateTime), IsDateTime);
            AppendProperty(sb, nameof(IsTimeSpan), IsTimeSpan);

            return sb
                .Append(" }")
                .ToString();

            static void AppendProperty(StringBuilder sb, string k, object p)
            {
                sb.Append(", ")
                    .Append('"')
                    .Append(k)
                    .Append("\": ")
                    .Append(p);
            }
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hc = new HashCode();
            GetDeepHashCode(ref hc);
            return hc.ToHashCode();
        }

        /// <inheritdoc/>
        public object Clone()
        {
            return Copy();
        }

        /// <summary>
        /// Convert to json
        /// </summary>
        public string ToJson()
        {
            return AppendTo(new StringBuilder()).ToString();
        }

        /// <summary>
        /// Convert value to typed value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultValue"></param>
        public T ConvertTo<T>(T defaultValue)
        {
            var typed = ConvertTo(typeof(T));
            return typed == null ? defaultValue : (T)typed;
        }

        /// <summary>
        /// Convert value to typed value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public T? ConvertTo<T>()
        {
            var typed = ConvertTo(typeof(T));
            return typed == null ? default : (T)typed;
        }

        /// <summary>
        /// Convert value to typed value
        /// </summary>
        /// <param name="type"></param>
        public abstract object? ConvertTo(Type type);

        /// <summary>
        /// Update the value to the new value.
        /// </summary>
        /// <param name="value"></param>
        public abstract void AssignValue(object? value);

        /// <summary>
        /// Clone this item or entire tree
        /// </summary>
        /// <param name="shallow"></param>
        public abstract VariantValue Copy(bool shallow = false);

        /// <summary>
        /// Get value for property
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public virtual bool TryGetProperty(string key,
            [NotNullWhen(true)] out VariantValue? value)
        {
            value = null;
            return false;
        }

        /// <summary>
        /// Get value from array index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public virtual bool TryGetElement(int index,
            [NotNullWhen(true)] out VariantValue? value)
        {
            value = null;
            return false;
        }

        /// <summary>
        /// Try get primitive value
        /// </summary>
        /// <param name="o"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetValue([NotNullWhen(true)] out object? o,
            IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    o = null;
                    return false;
                case bool:
                case int:
                case long:
                case short:
                case uint:
                case ushort:
                case sbyte:
                case byte:
                case char:
                case ulong:
                case float:
                case double:
                case decimal:
                case BigInteger:
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                case Guid:
                case byte[]:
                    o = raw;
                    return true;
            }
            // Parse string
            if (TryGetBoolean(out var b, true, provider))
            {
                o = b;
                return true;
            }
            if (TryGetUInt64(out var ul, true, provider))
            {
                o = ul;
                return true;
            }
            if (TryGetInt64(out var l, true, provider))
            {
                o = l;
                return true;
            }
            if (TryGetSingle(out var f, true, provider))
            {
                o = f;
                return true;
            }
            if (TryGetDouble(out var d, true, provider))
            {
                o = d;
                return true;
            }
            if (TryGetDecimal(out var dec, true, provider))
            {
                o = dec;
                return true;
            }
            if (TryGetBigInteger(out var bi, true, provider))
            {
                o = bi;
                return true;
            }
            if (TryGetTimeSpan(out var ts, true, provider))
            {
                o = ts;
                return true;
            }
            if (TryGetDateTime(out var dt, true, provider))
            {
                o = dt;
                return true;
            }
            if (TryGetDateTimeOffset(out var dto, true, provider))
            {
                o = dto;
                return true;
            }
            if (TryGetGuid(out var g, true, provider))
            {
                o = g;
                return true;
            }
            if (TryGetString(out var s, true, provider))
            {
                o = s;
                return true;
            }
            if (TryGetBytes(out var buffer, true, provider))
            {
                o = buffer;
                return true;
            }
            o = null;
            return false;
        }

        /// <summary>
        /// Returns double value
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetDouble(out double o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0.0;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                case Guid:
                    return false;
                case char:
                case int:
                case uint:
                case long:
                case ulong:
                case short:
                case ushort:
                case sbyte:
                case byte:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToDouble(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case decimal:
                    try
                    {
                        o = Convert.ToDouble(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case BigInteger b:
                    if (strict)
                    {
                        return false;
                    }
                    o = (double)b;
                    return b.Equals(o);
                case float f:
                    o = f;
                    return true;
                case double d:
                    o = d;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            var result = true;
            if (s == kDoubleMinValue)
            {
                o = double.MinValue;
            }
            else if (s == kDoubleMaxValue)
            {
                o = double.MaxValue;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.NaNSymbol)
            {
                o = double.NaN;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.PositiveInfinitySymbol)
            {
                o = double.PositiveInfinity;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.NegativeInfinitySymbol)
            {
                o = double.NegativeInfinity;
            }
            else
            {
                result = double.TryParse(s, NumberStyles.Float, provider, out o);
                // Since .net 3 infinite means overflow
                result = result && !double.IsInfinity(o);
            }
            return result;
        }
        private static readonly string kDoubleMinValue =
            double.MinValue.ToString(CultureInfo.InvariantCulture);
        private static readonly string kDoubleMaxValue =
            double.MaxValue.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns float value
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetSingle(out float o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0.0f;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case char:
                    return false;
                case Guid:
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                    return false;
                case int:
                case uint:
                case long:
                case ulong:
                case short:
                case ushort:
                case sbyte:
                case byte:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToSingle(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case decimal:
                    try
                    {
                        o = Convert.ToSingle(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case BigInteger b:
                    if (strict)
                    {
                        return false;
                    }
                    o = (float)b;
                    return b.Equals(o);
                case float f:
                    o = f;
                    return true;
                case double d:
                    if (d is > float.MaxValue or < float.MinValue)
                    {
                        // Allow NaN and infinite as single as they are symbols.
                        if (strict && !double.IsNaN(d) && !double.IsInfinity(d))
                        {
                            return false;
                        }
                        s = d.ToString("G9", provider);
                        break;
                    }
                    o = (float)d;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            var result = true;
            if (s == kFloatMinValue)
            {
                o = float.MinValue;
            }
            else if (s == kFloatMaxValue)
            {
                o = float.MaxValue;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.NaNSymbol)
            {
                o = float.NaN;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.PositiveInfinitySymbol)
            {
                o = float.PositiveInfinity;
            }
            else if (s == CultureInfo.InvariantCulture.NumberFormat.NegativeInfinitySymbol)
            {
                o = float.NegativeInfinity;
            }
            else
            {
                result = float.TryParse(s, NumberStyles.Float, provider, out o);
                // Since .net 3 infinite means overflow
                result = result && !float.IsInfinity(o);
            }
            return result;
        }
        private static readonly string kFloatMinValue =
            float.MinValue.ToString(CultureInfo.InvariantCulture);
        private static readonly string kFloatMaxValue =
            float.MaxValue.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns decimal value
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetDecimal(out decimal o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0m;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                    return false;
                case TimeSpan ts:
                    if (strict)
                    {
                        return false;
                    }
                    o = Convert.ToDecimal(ts.Ticks, provider);
                    return true;
                case DateTime dt:
                    if (strict)
                    {
                        return false;
                    }
                    o = Convert.ToDecimal(dt.Ticks, provider);
                    return true;
                case DateTimeOffset dto:
                    if (strict)
                    {
                        return false;
                    }
                    o = Convert.ToDecimal(dto.UtcTicks, provider);
                    return true;
                case bool:
                case char:
                case int:
                case uint:
                case long:
                case ulong:
                case short:
                case ushort:
                case sbyte:
                case byte:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToDecimal(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case byte[] buf:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        var b = new BigInteger(buf);
                        o = (decimal)b;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case BigInteger b:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = (decimal)b;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case float:
                case double:
                    try
                    {
                        o = Convert.ToDecimal(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case decimal dec:
                    o = dec;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable f ? f.ToString("G", provider) : raw.ToString();
                    break;
            }
            return decimal.TryParse(s, strict ? NumberStyles.Float : NumberStyles.Any,
                provider, out o);
        }

        /// <summary>
        /// Returns signed integer
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetInt64(out long o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0L;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    return false;
                case Guid:
                case char:
                    return false;
                case TimeSpan ts:
                    if (strict)
                    {
                        return false;
                    }
                    o = ts.Ticks;
                    return true;
                case DateTime dt:
                    if (strict)
                    {
                        return false;
                    }
                    o = dt.Ticks;
                    return true;
                case DateTimeOffset dto:
                    if (strict)
                    {
                        return false;
                    }
                    o = dto.UtcTicks;
                    return true;
                case int:
                case long:
                case short:
                case uint:
                case ushort:
                case sbyte:
                case byte:
                    o = Convert.ToInt64(raw, provider);
                    return true;
                case ulong v:
                    if (v > long.MaxValue)
                    {
                        return false;
                    }
                    o = (long)v;
                    return true;
                case float:
                case double:
                case decimal:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToInt64(raw, provider);
                        return o.Equals(raw);
                    }
                    catch
                    {
                        return false;
                    }
                case byte[] buf:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = (long)new BigInteger(buf);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case BigInteger b:
                    try
                    {
                        o = (long)b;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable f ?
                        f.ToString("G", provider) : raw.ToString();
                    break;
            }
            return long.TryParse(s,
                NumberStyles.Integer, provider, out o);
        }

        /// <summary>
        /// Returns signed integer
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetUInt64(out ulong o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0UL;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    return false;
                case Guid:
                case char:
                    return false;
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                    return false;
                case int:
                case long:
                case short:
                case sbyte:
                    var signed = Convert.ToInt64(raw, provider);
                    if (signed < 0)
                    {
                        return false;
                    }
                    o = Convert.ToUInt64(raw, provider);
                    return true;
                case uint:
                case ushort:
                case byte:
                    o = Convert.ToUInt64(raw, provider);
                    return true;
                case ulong v:
                    o = v;
                    return true;
                case float:
                case double:
                case decimal:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToUInt64(raw, provider);
                        return o.Equals(raw);
                    }
                    catch
                    {
                        return false;
                    }
                case byte[] buf:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = (ulong)new BigInteger(buf);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case BigInteger b:
                    try
                    {
                        o = (ulong)b;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable f ?
                        f.ToString("G", provider) : raw.ToString();
                    break;
            }
            return ulong.TryParse(s, NumberStyles.Integer, provider, out o);
        }

        /// <summary>
        /// Returns byte
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetByte(out byte o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = 0;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    return false;
                case Guid:
                case char:
                    return false;
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                    return false;
                case int:
                case long:
                case short:
                case sbyte:
                case uint:
                case ushort:
                case ulong:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToByte(raw, provider);
                        return o.Equals(raw);
                    }
                    catch
                    {
                        return false;
                    }

                case byte b:
                    o = b;
                    return true;
                case float:
                case double:
                case decimal:
                case BigInteger:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = (byte)raw;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable f ?
                        f.ToString("G", provider) : raw.ToString();
                    break;
            }
            return byte.TryParse(s, NumberStyles.Integer, provider, out o);
        }

        /// <summary>
        /// Returns character
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetChar(out char o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = char.MinValue;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                    return false;
                case char c:
                    o = c;
                    return true;
                case int:
                case long:
                case short:
                case sbyte:
                case uint:
                case ushort:
                case byte:
                case ulong:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = Convert.ToChar(raw, provider);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case float:
                case double:
                case decimal:
                    return false;
                case BigInteger:
                    return false;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable f ?
                        f.ToString("G", provider) : raw.ToString();
                    break;
            }
            if (char.TryParse(s, out o))
            {
                return true;
            }
            if (s is null)
            {
                return false;
            }
            if (s.Length == 1)
            {
                o = s[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get value as integer
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetBigInteger(out BigInteger o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = BigInteger.Zero;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    return false;
                case Guid:
                case char:
                    return false;
                case TimeSpan:
                case DateTime:
                case DateTimeOffset:
                    return false;
                case uint:
                case ulong:
                case ushort:
                case byte:
                    o = new BigInteger(Convert.ToUInt64(raw, provider));
                    return true;
                case int:
                case long:
                case short:
                case sbyte:
                    o = new BigInteger(Convert.ToInt64(raw, provider));
                    return true;
                case BigInteger b:
                    o = b;
                    return true;
                case byte[] buf:
                    if (strict)
                    {
                        return false;
                    }
                    o = new BigInteger(buf);
                    return true;
                case decimal dec:
                    if (strict)
                    {
                        return false;
                    }
                    o = new BigInteger(dec);
                    return decimal.Floor(dec).Equals(dec);
                case float f:
                    if (strict)
                    {
                        return false;
                    }
                    if (float.IsNaN(f) || float.IsInfinity(f))
                    {
                        return false;
                    }
                    o = new BigInteger(f);
                    return Math.Floor(f).Equals(f);
                case double d:
                    if (strict)
                    {
                        return false;
                    }
                    if (double.IsNaN(d) || double.IsInfinity(d))
                    {
                        return false;
                    }
                    o = new BigInteger(d);
                    return Math.Floor(d).Equals(d);
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return BigInteger.TryParse(s, NumberStyles.Integer,
                provider, out o);
        }

        /// <summary>
        /// Get value as timespan
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetTimeSpan(out TimeSpan o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = TimeSpan.MinValue;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                    return false;
                case long l:
                    if (strict)
                    {
                        return false;
                    }
                    o = TimeSpan.FromTicks(l);
                    return true;
                case TimeSpan ts:
                    o = ts;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return TimeSpan.TryParse(s, provider, out o);
        }

        /// <summary>
        /// Get value as date
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        protected virtual bool TryGetDateTime(out DateTime o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = DateTime.MinValue;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                    return false;
                case DateTime dt:
                    o = dt;
                    return true;
                case long l:
                    if (strict)
                    {
                        return false;
                    }
                    try
                    {
                        o = DateTime.FromBinary(l);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                case DateTimeOffset dto:
                    o = dto.UtcDateTime;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return DateTime.TryParse(s, provider,
                DateTimeStyles.AdjustToUniversal, out o);
        }

        /// <summary>
        /// Get value as date time offset
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetDateTimeOffset(out DateTimeOffset o,
            bool strict = true, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = DateTimeOffset.MinValue;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                    return false;
                case DateTime dt:
                    if (strict)
                    {
                        return false;
                    }
                    o = dt;
                    return true;
                case long l:
                    if (strict)
                    {
                        return false;
                    }
                    o = DateTimeOffset.FromUnixTimeMilliseconds(l);
                    return true;
                case DateTimeOffset dto:
                    o = dto;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return DateTimeOffset.TryParse(s, provider,
                DateTimeStyles.AdjustToUniversal, out o);
        }

        /// <summary>
        /// Get value as boolean
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetBoolean(out bool o, bool strict = true,
            IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = false;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                case Guid:
                    return false;
                case bool b:
                    o = b;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return bool.TryParse(s, out o);
        }

        /// <summary>
        /// Get Value as guid
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetGuid(out Guid o, bool strict = true,
            IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = Guid.Empty;
            string? s;
            var raw = GetRawValue();
            switch (raw)
            {
                case null:
                    return false;
                case Guid g:
                    o = g;
                    return true;
                case string str:
                    s = str;
                    break;
                default:
                    if (GetValueType() != VariantValueType.Primitive)
                    {
                        return false;
                    }
                    s = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw.ToString();
                    break;
            }
            return Guid.TryParse(s, out o);
        }

        /// <summary>
        /// Get Value as a bytes type
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetBytes([NotNullWhen(true)] out byte[]? o,
            bool strict = true, IFormatProvider? provider = null)
        {
            o = null;
            if (GetValueType() == VariantValueType.Values)
            {
                // Convert array to bytes
                var buffer = new List<byte>();
                foreach (var item in GetArrayElements())
                {
                    if (!item.TryGetByte(out var b, true, CultureInfo.InvariantCulture))
                    {
                        return false;
                    }
                    buffer.Add(b);
                }
                o = buffer.ToArray();
                return true;
            }
            if (GetValueType() == VariantValueType.Complex)
            {
                return false;
            }
            switch (GetRawValue())
            {
                case null:
                    return false;
                case Guid g:
                    if (strict)
                    {
                        return false;
                    }
                    o = g.ToByteArray();
                    return true;
                case byte[] b:
                    o = b;
                    return true;
                case string s:
                    if (strict && s.Length == 0)
                    {
                        return false;
                    }
                    return TryFromBase64String(s, out o);
                default:
                    // Must be string or override
                    return false;
            }
        }

        /// <summary>
        /// Value is a string type
        /// </summary>
        /// <param name="o"></param>
        /// <param name="strict"></param>
        /// <param name="provider"></param>
        public virtual bool TryGetString([NotNullWhen(true)] out string? o, bool strict = true,
            IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            o = null;
            if (GetValueType() != VariantValueType.Primitive)
            {
                return false;
            }
            var raw = GetRawValue();
            switch (raw)
            {
                case string s:
                    o = s.ToString(provider);
                    return true;
                case Guid g:
                    if (strict)
                    {
                        return false;
                    }
                    o = g.ToString();
                    return true;
                case byte[] b:
                    if (strict)
                    {
                        return false;
                    }
                    o = Convert.ToBase64String(b);
                    return true;
                default:
                    if (strict)
                    {
                        return false;
                    }
                    o = raw is IFormattable fmt ?
                        fmt.ToString("G", provider) : raw?.ToString();
                    return o != null;
            }
        }

        /// <summary>
        /// Select value using path. Path can be either . seperated
        /// property names or contain index [] for array elements.
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual VariantValue GetByPath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            var leaf = this;
            foreach (var elem in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                var key = elem;
                var index = -1;
                var offset = elem.Split('[', StringSplitOptions.RemoveEmptyEntries);
                if (offset.Length > 1)
                {
                    var end = offset[1].IndexOf(']', StringComparison.InvariantCulture);
                    if (end != -1 &&
                        int.TryParse(offset[1][..end], out index))
                    {
                        key = offset[0];
                    }
                    else
                    {
                        index = -1;
                    }
                }
                if (!leaf.TryGetProperty(key, out leaf))
                {
                    return Null;
                }
                if (index != -1 && !leaf.TryGetElement(index, out leaf))
                {
                    return Null;
                }
            }
            return leaf;
        }

        /// <summary>
        /// Helper to try and get base 64 encoded buffer from string.
        /// </summary>
        /// <param name="base64"></param>
        /// <param name="result"></param>
        protected static bool TryFromBase64String(string base64,
            [NotNullWhen(true)] out byte[]? result)
        {
            var buffer = new Span<byte>(new byte[base64.Length]);
            var success = Convert.TryFromBase64String(base64, buffer, out var bytesWritten);
            result = success ? buffer[..bytesWritten].ToArray() : null;
            return success;
        }

        /// <summary>
        /// Value comparer
        /// </summary>
        public static IComparer<VariantValue> Comparer => new VariantValueComparer();

        /// <summary>
        /// Equality comparer
        /// </summary>
        public static IEqualityComparer<VariantValue> Equality => new VariantValueComparer();

        /// <inheritdoc/>
        internal sealed class VariantValueComparer : IEqualityComparer<VariantValue>, IComparer<VariantValue>
        {
            /// <inheritdoc/>
            public bool Equals(VariantValue? x, VariantValue? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                var yt = y?.GetValueType() ?? VariantValueType.Null;
                var xt = x?.GetValueType() ?? VariantValueType.Null;

                if (yt != xt)
                {
                    if (xt == VariantValueType.Null || yt == VariantValueType.Null)
                    {
                        return false;
                    }
                    // Special case
                    if (xt == VariantValueType.Primitive || yt == VariantValueType.Primitive)
                    {
                        if (xt == VariantValueType.Values || yt == VariantValueType.Values)
                        {
                            // Compare as bytes
                            if (x!.TryGetBytes(out var bufx, true, CultureInfo.InvariantCulture) &&
                                y!.TryGetBytes(out var bufy, true, CultureInfo.InvariantCulture) &&
                                bufx.AsSpan().SequenceEqual(bufy))
                            {
                                return true;
                            }
                        }

                        // Values or object compare to string
                        if (xt == VariantValueType.Primitive &&
                            x!.TryGetString(out var sx, true, CultureInfo.InvariantCulture) && y!.ToJson() == sx)
                        {
                            return true;
                        }
                        if (yt == VariantValueType.Primitive &&
                            y!.TryGetString(out var sy, true, CultureInfo.InvariantCulture) && x!.ToJson() == sy)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                if (xt == VariantValueType.Null)
                {
                    // If both null then they are the same
                    return true;
                }

                // Perform structural comparison
                if (xt == VariantValueType.Values)
                {
                    if (x!.GetArrayElements().SequenceEqual(y!.GetArrayElements(), Equality))
                    {
                        return true;
                    }
                    return false;
                }

                if (xt == VariantValueType.Complex)
                {
                    var px = x!.PropertyNames.OrderBy(k => k).Select(k => x[k]);
                    var py = y!.PropertyNames.OrderBy(k => k).Select(k => y[k]);
                    if (px.SequenceEqual(py, Equality))
                    {
                        return true;
                    }
                    return false;
                }

                // Allow implementation to perform comparison first
                if (x!.TryEqualsVariant(y, out var result) ||
                    y!.TryEqualsVariant(x, out result))
                {
                    if (result)
                    {
                        return true;
                    }
                    return false;
                }

                // Compare floating point values
                if (x.TryGetSingle(out var fx, true, CultureInfo.InvariantCulture) &&
                    y.TryGetSingle(out var fy, true, CultureInfo.InvariantCulture) &&
                    fx == fy)
                {
                    return true;
                }
                if (x.TryGetDouble(out var dx, true, CultureInfo.InvariantCulture) &&
                    y.TryGetDouble(out var dy, true, CultureInfo.InvariantCulture) &&
                    dx == dy)
                {
                    return true;
                }

                // Compare numbers - includes dates and times
                if (x.TryGetDecimal(out var nx, false, CultureInfo.InvariantCulture) &&
                    y.TryGetDecimal(out var ny, false, CultureInfo.InvariantCulture) &&
                    nx == ny)
                {
                    return true;
                }

                // Compare bytes - includes empty strings
                if (x.TryGetBytes(out var bx, false, CultureInfo.InvariantCulture) &&
                    y.TryGetBytes(out var by, false, CultureInfo.InvariantCulture) &&
                    bx.AsSpan().SequenceEqual(by))
                {
                    return true;
                }

                // Compare values
                if (y.TryGetValue(out var yv, CultureInfo.InvariantCulture) && EqualValues(x, yv))
                {
                    return true;
                }
                if (x.TryGetValue(out var xv, CultureInfo.InvariantCulture) && EqualValues(y, xv))
                {
                    return true;
                }
                return false;
            }

            /// <inheritdoc/>
            public int Compare(VariantValue? x, VariantValue? y)
            {
                var yt = y?.GetValueType() ?? VariantValueType.Null;
                var xt = x?.GetValueType() ?? VariantValueType.Null;

                if (yt != xt)
                {
                    if (xt != VariantValueType.Null && yt != VariantValueType.Null)
                    {
                        // Special case compare
                        if (xt == VariantValueType.Primitive || yt == VariantValueType.Primitive)
                        {
                            if (xt == VariantValueType.Values || yt == VariantValueType.Values)
                            {
                                // Compare as bytes
                                if (x!.TryGetBytes(out var bufx, true, CultureInfo.InvariantCulture) &&
                                    y!.TryGetBytes(out var bufy, true, CultureInfo.InvariantCulture))
                                {
                                    return string.CompareOrdinal(
                                        Convert.ToBase64String(bufx),
                                        Convert.ToBase64String(bufy));
                                }
                            }

                            // Values or object compare to string
                            if (xt == VariantValueType.Primitive &&
                                x!.TryGetString(out var sx, true, CultureInfo.InvariantCulture))
                            {
                                return string.CompareOrdinal(sx, y!.ToJson());
                            }
                            if (yt == VariantValueType.Primitive &&
                                y!.TryGetString(out var sy, true, CultureInfo.InvariantCulture))
                            {
                                return string.CompareOrdinal(x!.ToJson(), sy);
                            }
                        }
                    }
                    return xt.CompareTo(yt);
                }

                // First compare values to see if they are the same
                if (Equals(x, y))
                {
                    return 0;
                }

                // Allow implementation to perform comparison first
                if (x!.TryCompareToValue(y, out var result))
                {
                    return result < 0 ? -1 : result > 0 ? 1 : 0;
                }
                if (y!.TryCompareToValue(x, out result))
                {
                    return result > 0 ? -1 : result < 0 ? 1 : 0;
                }

                // Perform primitive value comparison
                if (xt == VariantValueType.Primitive)
                {
                    if (x.TryGetSingle(out var fx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetSingle(out var fy, true, CultureInfo.InvariantCulture))
                    {
                        return fx.CompareTo(fy);
                    }
                    if (x.TryGetDouble(out var dx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetDouble(out var dy, true, CultureInfo.InvariantCulture))
                    {
                        return dx.CompareTo(dy);
                    }
                    if (x.TryGetDecimal(out var decx, false, CultureInfo.InvariantCulture) &&
                        y.TryGetDecimal(out var decy, false, CultureInfo.InvariantCulture))
                    {
                        return decx.CompareTo(decy);
                    }
                    if (x.TryGetBigInteger(out var bix, true, CultureInfo.InvariantCulture) &&
                        y.TryGetBigInteger(out var biy, true, CultureInfo.InvariantCulture))
                    {
                        return bix.CompareTo(biy);
                    }
                    if (x.TryGetTimeSpan(out var tx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetTimeSpan(out var ty, true, CultureInfo.InvariantCulture))
                    {
                        return tx.CompareTo(ty);
                    }
                    if (x.TryGetDateTimeOffset(out var dtox, true, CultureInfo.InvariantCulture) &&
                        y.TryGetDateTimeOffset(out var dtoy, true, CultureInfo.InvariantCulture))
                    {
                        return dtox.CompareTo(dtoy);
                    }
                    if (x.TryGetDateTime(out var dtx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetDateTime(out var dty, true, CultureInfo.InvariantCulture))
                    {
                        return dtx.CompareTo(dty);
                    }
                    if (x.TryGetGuid(out var gx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetGuid(out var gy, true, CultureInfo.InvariantCulture))
                    {
                        return gx.CompareTo(gy);
                    }
                    if (x.TryGetString(out var sx, true, CultureInfo.InvariantCulture) &&
                        y.TryGetString(out var sy, true, CultureInfo.InvariantCulture))
                    {
                        return string.CompareOrdinal(sx, sy);
                    }
                }

                // Use string comparison
                var osx = x.ToJson().ToUpperInvariant();
                var osy = y.ToJson().ToUpperInvariant();

                return string.CompareOrdinal(osx, osy);
            }

            /// <inheritdoc/>
            public int GetHashCode(VariantValue obj)
            {
                return obj?.GetHashCode() ?? 0;
            }

            /// <summary>
            /// Tries to compare equality of 2 values using convertible
            /// and comparable interfaces.
            /// </summary>
            /// <param name="v"></param>
            /// <param name="y"></param>
            internal static bool EqualValues(VariantValue v, object? y)
            {
                // Allow implementation to perform comparison first
                if (v.TryEqualsValue(y, out var equality))
                {
                    return equality;
                }

                if (v.GetValueType() != VariantValueType.Primitive)
                {
                    if (y is string s)
                    {
                        return v.ToJson() == s;
                    }
                    if (y is byte[] boy && v.GetValueType() == VariantValueType.Values &&
                        v.TryGetBytes(out var box, true, CultureInfo.InvariantCulture))
                    {
                        return box.AsSpan().SequenceEqual(boy);
                    }
                }

                if (!v.TryGetValue(out var x, CultureInfo.InvariantCulture))
                {
                    try
                    {
                        x = v.ConvertTo(y!.GetType());
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                if (y.Equals(x) || x.Equals(y))
                {
                    return true;
                }

                if (x is byte[] bx && y is byte[] by)
                {
                    return bx.AsSpan().SequenceEqual(by);
                }

                if (x is IConvertible co1)
                {
                    try
                    {
                        var compare = co1.ToType(y.GetType(),
                            CultureInfo.InvariantCulture);
                        if (!compare.Equals(y))
                        {
                            return false;
                        }
                        return true;
                    }
                    catch
                    {
                    }
                }
                if (y is IConvertible co2)
                {
                    try
                    {
                        var compare = co2.ToType(x.GetType(),
                            CultureInfo.InvariantCulture);
                        if (!compare.Equals(x))
                        {
                            return false;
                        }
                        return true;
                    }
                    catch
                    {
                    }
                }

                // Compare values through comparison operation
                return TryCompare(v, y, out var result, false) && result == 0;
            }

            /// <summary>
            /// Compare value
            /// </summary>
            /// <param name="v"></param>
            /// <param name="y"></param>
            internal static int CompareValues(VariantValue v, object? y)
            {
                if (TryCompare(v, y, out var result, true))
                {
                    return result;
                }
                return -1;
            }

            /// <summary>
            /// Compare
            /// </summary>
            /// <param name="v"></param>
            /// <param name="y"></param>
            /// <param name="result"></param>
            /// <param name="stringCompare"></param>
            private static bool TryCompare(VariantValue v, object? y,
                out int result, bool stringCompare = false)
            {
                // Allow implementation to perform comparison first
                if (v.TryCompareToValue(y, out result))
                {
                    return true;
                }

                if (v.GetValueType() != VariantValueType.Primitive)
                {
                    if (y is string s)
                    {
                        result = string.CompareOrdinal(v.ToJson(), s);
                        return true;
                    }
                    if (y is byte[] boy && v.GetValueType() == VariantValueType.Values &&
                        v.TryGetBytes(out var box, true, CultureInfo.InvariantCulture))
                    {
                        var box64 = Convert.ToBase64String(box);
                        var boy64 = Convert.ToBase64String(boy);
                        result = string.CompareOrdinal(box64, boy64);
                        return true;
                    }
                }

                if (!v.TryGetValue(out var x, CultureInfo.InvariantCulture))
                {
                    // Throw if needed
                    x = v.ConvertTo(y!.GetType());
                }

                if (TryCompare(x, y, out result))
                {
                    result = result < 0 ? -1 : result > 0 ? 1 : 0;
                    return true;
                }
                if (TryCompare(y, x, out result))
                {
                    result = result > 0 ? -1 : result < 0 ? 1 : 0;
                    return true;
                }

                if (!stringCompare)
                {
                    result = -1;
                    return false;
                }

                // Compare stringified version
                var s1 = x?.ToString() ?? "null";
                var s2 = y?.ToString() ?? "null";
                result = string.CompareOrdinal(s1, s2);
                return true;
            }

            /// <summary>
            /// Compare
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="result"></param>
            private static bool TryCompare(object? x, object? y, out int result)
            {
                if (x is IComparable cv1)
                {
                    try
                    {
                        if (x.GetType() != y?.GetType() && y is IConvertible c)
                        {
                            y = c.ToType(x.GetType(), CultureInfo.InvariantCulture);
                        }
                        result = cv1.CompareTo(y);
                        return true;
                    }
                    catch
                    {
                    }
                }
                result = -1;
                return false;
            }
        }

        /// <summary>
        /// Represents a primitive value for assignment purposes
        /// </summary>
        internal sealed class PrimitiveValue : VariantValue
        {
            /// <inheritdoc/>
            protected override VariantValueType GetValueType()
            {
                return _valueType;
            }

            /// <inheritdoc/>
            protected override IEnumerable<string> GetObjectProperties()
            {
                return Enumerable.Empty<string>();
            }

            /// <inheritdoc/>
            protected override object? GetRawValue()
            {
                return _rawValue;
            }

            /// <inheritdoc/>
            protected override IEnumerable<VariantValue> GetArrayElements()
            {
                return Enumerable.Empty<VariantValue>();
            }

            /// <inheritdoc/>
            protected override int GetArrayCount()
            {
                return 0;
            }

            /// <summary>
            /// Clone
            /// </summary>
            /// <param name="value"></param>
            /// <param name="type"></param>
            internal PrimitiveValue(object? value, VariantValueType type)
            {
                _rawValue = value;
                _valueType = value == null ? VariantValueType.Null : type;
            }

            /// <inheritdoc/>
            public PrimitiveValue(string? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(byte[]? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(bool value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(byte value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(sbyte value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(short value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(ushort value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(int value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(uint value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(long value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(ulong value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(float value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(double value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(decimal value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(Guid value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(BigInteger value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(DateTime value) :
                this(value.Kind == DateTimeKind.Local ?
                    value.ToUniversalTime() : value,
                    VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(DateTimeOffset value) :
                this(value.Offset != TimeSpan.Zero ?
                    value.ToUniversalTime() : value,
                    VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(TimeSpan value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(bool? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(byte? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(sbyte? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(short? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(ushort? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(int? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(uint? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(long? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(ulong? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(float? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(double? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(decimal? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(Guid? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(BigInteger? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(DateTime? value) :
                this(!value.HasValue ? value :
                    value.Value.Kind == DateTimeKind.Local ?
                    value.Value.ToUniversalTime() : value.Value,
                    VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(DateTimeOffset? value) :
                this(!value.HasValue ? value :
                    value.Value.Offset != TimeSpan.Zero ?
                    value.Value.ToUniversalTime() : value.Value,
                    VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public PrimitiveValue(TimeSpan? value) :
                this(value, VariantValueType.Primitive)
            {
            }

            /// <inheritdoc/>
            public override VariantValue Copy(bool shallow = false)
            {
                return new PrimitiveValue(_rawValue, GetValueType());
            }

            /// <inheritdoc/>
            public override object? ConvertTo(Type type)
            {
                if (_rawValue == null || IsNull)
                {
                    if (type.IsValueType)
                    {
                        return Activator.CreateInstance(type);
                    }
                    return null;
                }
                if (type.IsAssignableFrom(_rawValue.GetType()))
                {
                    return _rawValue;
                }
                if (_rawValue is IConvertible c)
                {
                    return c.ToType(type, CultureInfo.InvariantCulture);
                }
                var converter = TypeDescriptor.GetConverter(type);
                return converter.ConvertFrom(_rawValue);
            }

            /// <inheritdoc/>
            public override VariantValue GetByPath(string path)
            {
                return Null;
            }

            /// <inheritdoc/>
            public override void AssignValue(object? value)
            {
                throw new NotSupportedException("Not an object");
            }

            /// <inheritdoc/>
            protected override VariantValue AddProperty(string propertyName)
            {
                throw new NotSupportedException("Not an object");
            }

            private readonly VariantValueType _valueType;
            private readonly object? _rawValue;
        }

        /// <summary>
        /// Create value which is set to null.
        /// </summary>
        /// <param name="propertyName"></param>
        protected abstract VariantValue AddProperty(string propertyName);

        /// <summary>
        /// Get type of value
        /// </summary>
        protected abstract VariantValueType GetValueType();

        /// <summary>
        /// Provide raw value or null
        /// </summary>
        protected abstract object? GetRawValue();

        /// <summary>
        /// Values of array
        /// </summary>
        protected abstract IEnumerable<VariantValue> GetArrayElements();

        /// <summary>
        /// Length of array
        /// </summary>
        protected abstract int GetArrayCount();

        /// <summary>
        /// Property names of object
        /// </summary>
        protected abstract IEnumerable<string> GetObjectProperties();

        /// <summary>
        /// Compare to a non variant value object, e.g. the value of
        /// another variant.  This can be overridden.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="equality"></param>
        protected virtual bool TryEqualsValue(object? o, out bool equality)
        {
            equality = false;
            return false;
        }

        /// <summary>
        /// Try to compare equality with another variant.
        /// The implementation should return false if comparison
        /// is not possible and must not call equality test
        /// with value itself.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="equality"></param>
        protected virtual bool TryEqualsVariant(VariantValue? v, out bool equality)
        {
            equality = false;
            return false;
        }

        /// <summary>
        /// Compare value
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="result"></param>
        protected virtual bool TryCompareToValue(object? obj, out int result)
        {
            result = 0;
            return false;
        }

        /// <summary>
        /// Compare variant value
        /// </summary>
        /// <param name="v"></param>
        /// <param name="result"></param>
        protected virtual bool TryCompareToVariantValue(VariantValue? v,
            out int result)
        {
            result = 0;
            return false;
        }

        /// <summary>
        /// Convert to string
        /// </summary>
        /// <param name="builder"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected virtual StringBuilder AppendTo(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            string? s;
            switch (GetValueType())
            {
                case VariantValueType.Null:
                    return builder.Append("null");
                case VariantValueType.Values:
                    var first = true;
                    builder = builder.Append('[');
                    foreach (var value in GetArrayElements())
                    {
                        if (!first)
                        {
                            builder = builder.Append(',');
                        }
                        else
                        {
                            first = false;
                        }
                        builder = value.AppendTo(builder);
                    }
                    return builder.Append(']');
                case VariantValueType.Complex:
                    var open = true;
                    builder = builder.Append('{');
                    foreach (var k in PropertyNames.OrderBy(k => k))
                    {
                        if (!open)
                        {
                            builder = builder.Append(',');
                        }
                        else
                        {
                            open = false;
                        }
                        builder = builder.Append(k)
                            .Append(':');
                        builder = this[k].AppendTo(builder);
                    }
                    return builder.Append('}');
                case VariantValueType.Primitive:
                    break;
            }
            if (!TryGetValue(out var raw, CultureInfo.InvariantCulture))
            {
                raw = GetRawValue();
            }
            switch (raw)
            {
                case string str:
                    s = str;
                    break;
                case byte[] b:
                    s = Convert.ToBase64String(b);
                    break;
                case Guid g:
                    s = g.ToString();
                    break;
                case DateTime dt:
                    s = dt.ToString("O", CultureInfo.InvariantCulture);
                    break;
                case DateTimeOffset dto:
                    s = dto.ToString("O", CultureInfo.InvariantCulture);
                    break;
                case TimeSpan ts:
                    s = ts.ToString("c", CultureInfo.InvariantCulture);
                    break;
                case BigInteger bi:
                    return builder.Append(bi.ToString("R", CultureInfo.InvariantCulture));
                case decimal d:
                    return builder.Append(d.ToString("G", CultureInfo.InvariantCulture));
                case double d:
                    s = d.ToString("G17", CultureInfo.InvariantCulture);
                    if (double.IsNaN(d) || double.IsInfinity(d))
                    {
                        break;
                    }
                    return builder.Append(s);
                case float f:
                    s = f.ToString("G9", CultureInfo.InvariantCulture);
                    if (float.IsNaN(f) || float.IsInfinity(f))
                    {
                        break;
                    }
                    return builder.Append(s);
                case int i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case long i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case short i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case uint i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case ushort i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case sbyte i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case byte i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case ulong i:
                    return builder.Append(i.ToString("G", CultureInfo.InvariantCulture));
                case bool:
                    return builder.Append(GetRawValue()?.ToString()?.ToUpperInvariant());
                default:
                    s = raw?.ToString();
                    break;
            }
            return builder.Append('"')
                .Append(s)
                .Append('"');
        }

        /// <summary>
        /// Create hash code for this or entire tree.
        /// </summary>
        /// <param name="hc"></param>
        private void GetDeepHashCode(ref HashCode hc)
        {
            switch (GetValueType())
            {
                case VariantValueType.Null:
                    hc.Add(GetValueType());
                    break;
                case VariantValueType.Primitive:
                    if (!TryGetValue(out var o, CultureInfo.InvariantCulture))
                    {
                        o = GetRawValue();
                    }
                    if (o is byte[] b)
                    {
                        o = Convert.ToBase64String(b);
                    }
                    hc.Add(o);
                    break;
                case VariantValueType.Values:
                    foreach (var value in GetArrayElements())
                    {
                        value.GetDeepHashCode(ref hc);
                    }
                    break;
                case VariantValueType.Complex:
                    foreach (var k in PropertyNames.OrderBy(k => k))
                    {
                        hc.Add(k);
                        this[k].GetDeepHashCode(ref hc);
                    }
                    break;
                default:
                    hc.Add(GetRawValue());
                    break;
            }
        }
    }
}
