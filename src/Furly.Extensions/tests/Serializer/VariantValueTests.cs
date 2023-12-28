// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers
{
    using System;
    using System.Globalization;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class VariantValueTests
    {
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Maintainability", "CA1508:Avoid dead conditional code", Justification = "Test")]
        public void NullCompareTests()
        {
            VariantValue? i1 = null;
            VariantValue? i2 = null;
            VariantValue i3 = "test";
            VariantValue i4 = 0;
            VariantValue i5 = TimeSpan.FromSeconds(1);

            Assert.True(i1 is null);
            Assert.Null(i1);
            Assert.Null(i1);
            Assert.True(i1 == i2);
            Assert.True(i1 != i3);
            Assert.True(i3 != i1);
            Assert.True(i1 != i4);
            Assert.True(i4 != i1);
            Assert.NotNull(i4);
            Assert.NotNull(i4);
            Assert.NotNull(i3);
            Assert.NotNull(i3);
            Assert.NotNull(i5);
            Assert.NotNull(i5);
        }

        [Fact]
        public void IntCompareTests()
        {
            VariantValue i1 = 1;
            VariantValue i2 = 2;
            VariantValue i3 = 2;

            Assert.True(i1 < i2);
            Assert.True(i1 <= i2);
            Assert.True(i2 > i1);
            Assert.True(i2 >= i1);
            Assert.True(i2 < 3);
            Assert.True(i2 <= 3);
            Assert.True(i2 <= 2);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= 2);
            Assert.True(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.Equal(1, i1);
            Assert.True(i2 == i3);
            Assert.NotEqual(2, i1);
            Assert.False(i2 == i1);
            Assert.NotEqual(2, i1);
        }

        [Fact]
        public void TimeSpanCompareTests()
        {
            VariantValue i1 = TimeSpan.FromSeconds(1);
            VariantValue i2 = TimeSpan.FromSeconds(2);
            VariantValue i3 = TimeSpan.FromSeconds(2);

            Assert.True(i1 < i2);
            Assert.True(i1 <= i2);
            Assert.True(i2 > i1);
            Assert.True(i2 >= i1);
            Assert.True(i2 < TimeSpan.FromSeconds(3));
            Assert.True(i2 <= TimeSpan.FromSeconds(3));
            Assert.True(i2 <= TimeSpan.FromSeconds(2));
            Assert.True(i2 <= i3);
            Assert.True(i2 >= TimeSpan.FromSeconds(2));
            Assert.True(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.True(i1 == TimeSpan.FromSeconds(1));
            Assert.True(i2 == i3);
            Assert.True(i1 != TimeSpan.FromSeconds(2));
            Assert.False(i2 == i1);
            Assert.False(i1 == TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void DateCompareTests()
        {
            VariantValue i1 = DateTime.MinValue;
            VariantValue i2 = DateTime.UtcNow;
            var i2a = i2.Copy();
            VariantValue i3 = DateTime.MaxValue;

            Assert.True(i1 < i2);
            Assert.True(i1 <= i2);
            Assert.True(i2 > i1);
            Assert.True(i2 >= i1);
            Assert.True(i2 < DateTime.MaxValue);
            Assert.True(i2 <= DateTime.MaxValue);
            Assert.True(i2 <= DateTime.UtcNow);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= i2a);
            Assert.True(i2 == i2a);
            Assert.True(i2 >= DateTime.MinValue);
            Assert.False(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.True(i1 == DateTime.MinValue);
            Assert.False(i2 == i3);
            Assert.True(i2 != i3);
            Assert.True(i1 != DateTime.UtcNow);
            Assert.False(i2 == i1);
            Assert.False(i1 == DateTime.UtcNow);
        }

        [Fact]
        public void FloatCompareTests()
        {
            VariantValue i1 = -0.123f;
            VariantValue i2 = 0.0f;
            VariantValue i2a = 0.0f;
            VariantValue i3 = 0.123f;

            Assert.True(i1 < i2);
            Assert.True(i1 <= i2);
            Assert.True(i2 > i1);
            Assert.True(i2 >= i1);
            Assert.True(i2 < 0.123f);
            Assert.True(i2 <= 0.123f);
            Assert.True(i2 <= 0.0f);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= i2a);
            Assert.True(i2 == i2a);
            Assert.True(i2 >= -0.123f);
            Assert.False(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.True(i1 == -0.123f);
            Assert.False(i2 == i3);
            Assert.True(i2 != i3);
            Assert.NotEqual(0.0f, i1);
            Assert.False(i2 == i1);
            Assert.NotEqual(0.0f, i1);
        }

        [Fact]
        public void DecimalCompareTests()
        {
            VariantValue i1 = -0.123m;
            VariantValue i2 = 0.0m;
            VariantValue i2a = 0.0m;
            VariantValue i3 = 0.123m;

            Assert.True(i1 < i2);
            Assert.True(i1 <= i2);
            Assert.True(i2 > i1);
            Assert.True(i2 >= i1);
            Assert.True(i2 < 0.123m);
            Assert.True(i2 < 0.123f);
            Assert.True(i2 <= 0.123m);
            Assert.True(i2 <= 0.123f);
            Assert.True(i2 <= 0.0m);
            Assert.True(i2 <= 0.0f);
            Assert.True(i2 <= 0.0);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= i2a);
            Assert.True(i2 == i2a);
            Assert.True(i2 >= -0.123m);
            Assert.False(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.True(i1 == -0.123m);
            Assert.True(i1 == -0.123f);
            Assert.False(i2 == i3);
            Assert.True(i2 != i3);
            Assert.NotEqual(0.0m, i1);
            Assert.NotEqual(0.0f, i1);
            Assert.False(i2 == i1);
            Assert.NotEqual(0.0m, i1);
        }

        [Fact]
        public void UlongCompareTests()
        {
            VariantValue i1 = 1ul;
            VariantValue i2 = 2ul;
            VariantValue i3 = 2ul;

            Assert.True(i1 < i2);
            Assert.True(i2 > i1);
            Assert.True(i2 < 3);
            Assert.True(i2 <= 2);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= 2);
            Assert.True(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.Equal(1, i1);
            Assert.True(i1 >= 1);
            Assert.True(i1 <= 1);
            Assert.True(i2 == i3);
            Assert.NotEqual(2, i1);
            Assert.True(i1 <= 2);
            Assert.False(i2 == i1);
            Assert.NotEqual(2, i1);
        }

        [Fact]
        public void UlongAndIntGreaterThanTests()
        {
            VariantValue i1 = -1;
            VariantValue i2 = 2ul;
            VariantValue i3 = 2;

            Assert.True(i1 < i2);
            Assert.True(i2 > i1);
            Assert.True(i2 < 3);
            Assert.True(i2 <= 2);
            Assert.True(i2 >= 2);
            Assert.True(i2 <= i3);
            Assert.True(i2 >= i3);
            Assert.True(i2 != i1);
            Assert.True(i1 < 0);
            Assert.True(i1 <= 0);
            Assert.True(i1 == -1);
            Assert.True(i1 >= -1);
            Assert.True(i1 <= -1);
            Assert.True(i2 == i3);
            Assert.NotEqual(2, i1);
            Assert.False(i2 == i1);
            Assert.NotEqual(2, i1);
        }

        [Fact]
        public void TypeCodeDoubleTests1()
        {
            const double floatMax = 3.40282357E+38;

            VariantValue i1 = floatMax;
            Assert.Equal(TypeCode.Double, i1.GetTypeCode());
            VariantValue i2 = floatMax.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i2.GetTypeCode());
            VariantValue i3 = floatMax;
            Assert.Equal(TypeCode.Double, i3.GetTypeCode());
            VariantValue i4 = floatMax.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i4.GetTypeCode());
        }

        [Fact]
        public void TypeCodeDoubleTests2()
        {
            const double floatMin = -3.40282357E+38;

            VariantValue i1 = floatMin;
            Assert.Equal(TypeCode.Double, i1.GetTypeCode());
            VariantValue i2 = floatMin.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i2.GetTypeCode());
            VariantValue i3 = floatMin;
            Assert.Equal(TypeCode.Double, i3.GetTypeCode());
            VariantValue i4 = floatMin.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i4.GetTypeCode());
        }

        [Fact]
        public void TypeCodeDoubleTests3()
        {
            VariantValue i1 = double.MaxValue;
            Assert.Equal(TypeCode.Double, i1.GetTypeCode());
            VariantValue i2 = double.MaxValue.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i2.GetTypeCode());
            VariantValue i3 = double.MinValue;
            Assert.Equal(TypeCode.Double, i3.GetTypeCode());
            VariantValue i4 = double.MinValue.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Double, i4.GetTypeCode());

            VariantValue i5 = double.PositiveInfinity;
            Assert.Equal(TypeCode.Single, i5.GetTypeCode());
            VariantValue i6 = double.NegativeInfinity;
            Assert.Equal(TypeCode.Single, i6.GetTypeCode());
            VariantValue i7 = double.NaN;
            Assert.Equal(TypeCode.Single, i7.GetTypeCode());
        }

        [Fact]
        public void TypeCodeSingleTests1()
        {
            VariantValue i1 = 0.0f;
            Assert.Equal(TypeCode.Single, i1.GetTypeCode());
            VariantValue i2 = "0.0";
            Assert.Equal(TypeCode.Single, i2.GetTypeCode());
            VariantValue i3 = 0.0;
            Assert.Equal(TypeCode.Single, i3.GetTypeCode());
        }

        [Fact]
        public void TypeCodeSingleTests2()
        {
            VariantValue i1 = float.MaxValue;
            Assert.Equal(TypeCode.Single, i1.GetTypeCode());
            VariantValue i2 = float.MaxValue.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Single, i2.GetTypeCode());
            VariantValue i3 = float.MinValue;
            Assert.Equal(TypeCode.Single, i3.GetTypeCode());
            VariantValue i4 = float.MinValue.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(TypeCode.Single, i4.GetTypeCode());
        }

        [Fact]
        public void TypeCodeSingleTests3()
        {
            VariantValue i1 = CultureInfo.InvariantCulture.NumberFormat.NaNSymbol;
            Assert.Equal(TypeCode.Single, i1.GetTypeCode());
            VariantValue i2 = CultureInfo.InvariantCulture.NumberFormat.PositiveInfinitySymbol;
            Assert.Equal(TypeCode.Single, i2.GetTypeCode());
            VariantValue i3 = CultureInfo.InvariantCulture.NumberFormat.NegativeInfinitySymbol;
            Assert.Equal(TypeCode.Single, i3.GetTypeCode());

            VariantValue i5 = float.PositiveInfinity;
            Assert.Equal(TypeCode.Single, i5.GetTypeCode());
            VariantValue i6 = float.NegativeInfinity;
            Assert.Equal(TypeCode.Single, i6.GetTypeCode());
            VariantValue i7 = float.NaN;
            Assert.Equal(TypeCode.Single, i7.GetTypeCode());
        }
    }
}
