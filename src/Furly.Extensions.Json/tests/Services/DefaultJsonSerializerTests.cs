// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Json
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class DefaultJsonSerializerTests
    {
        public virtual ISerializer Serializer => new DefaultJsonSerializer();

        public static IEnumerable<(VariantValue, object)> GetStrings()
        {
            yield return ("", "");
            yield return ("str ing", "str ing");
            yield return ("{}", "{}");
            yield return (Array.Empty<byte>(), Array.Empty<byte>());
            yield return (new byte[1000], new byte[1000]);
            yield return (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            yield return (Encoding.UTF8.GetBytes("utf-8-string"), Encoding.UTF8.GetBytes("utf-8-string"));
        }

        public static IEnumerable<(VariantValue, object?)> GetValues()
        {
            yield return (true, true);
            yield return (false, false);
            yield return ((bool?)null, (bool?)null);
            yield return ((sbyte)1, (sbyte)1);
            yield return ((sbyte)-1, (sbyte)-1);
            yield return ((sbyte)0, (sbyte)0);
            yield return (sbyte.MaxValue, sbyte.MaxValue);
            yield return (sbyte.MinValue, sbyte.MinValue);
            yield return ((sbyte?)null, (sbyte?)null);
            yield return ((short)1, (short)1);
            yield return ((short)-1, (short)-1);
            yield return ((short)0, (short)0);
            yield return (short.MaxValue, short.MaxValue);
            yield return (short.MinValue, short.MinValue);
            yield return ((short?)null, (short?)null);
            yield return (1, 1);
            yield return (-1, -1);
            yield return (0, 0);
            yield return (int.MaxValue, int.MaxValue);
            yield return (int.MinValue, int.MinValue);
            yield return ((int?)null, (int?)null);
            yield return (1L, 1L);
            yield return (-1L, -1L);
            yield return (0L, 0L);
            yield return (long.MaxValue, long.MaxValue);
            yield return (long.MinValue, long.MinValue);
            yield return ((long?)null, (long?)null);
            yield return (1UL, 1UL);
            yield return (0UL, 0UL);
            yield return (ulong.MaxValue, ulong.MaxValue);
            yield return ((ulong?)null, (ulong?)null);
            yield return (1u, 1u);
            yield return (0u, 0u);
            yield return (uint.MaxValue, uint.MaxValue);
            yield return ((uint?)null, (uint?)null);
            yield return ((ushort)1, (ushort)1);
            yield return ((ushort)0, (ushort)0);
            yield return (ushort.MaxValue, ushort.MaxValue);
            yield return ((ushort?)null, (ushort?)null);
            yield return ((byte)1, (byte)1);
            yield return ((byte)0, (byte)0);
            yield return (1.0, 1.0);
            yield return (-1.0, -1.0);
            yield return (0.0, 0.0);
            yield return (byte.MaxValue, byte.MaxValue);
            yield return ((byte?)null, (byte?)null);
            yield return (double.MaxValue, double.MaxValue);
            yield return (double.MinValue, double.MinValue);
            yield return (double.PositiveInfinity, double.PositiveInfinity);
            yield return (double.NegativeInfinity, double.NegativeInfinity);
            yield return ((double?)null, (double?)null);
            yield return (1.0f, 1.0f);
            yield return (-1.0f, -1.0f);
            yield return (0.0f, 0.0f);
            yield return (float.MaxValue, float.MaxValue);
            yield return (float.MinValue, float.MinValue);
            yield return (float.PositiveInfinity, float.PositiveInfinity);
            yield return (float.NegativeInfinity, float.NegativeInfinity);
            yield return ((float?)null, (float?)null);
            yield return ((decimal)1.0, (decimal)1.0);
            yield return ((decimal)-1.0, (decimal)-1.0);
            yield return ((decimal)0.0, (decimal)0.0);
            yield return ((decimal)1234567, (decimal)1234567);
            yield return ((decimal?)null, (decimal?)null);
            //  yield return (decimal.MaxValue, decimal.MaxValue);
            //  yield return (decimal.MinValue, decimal.MinValue);
            var guid = Guid.NewGuid();
            yield return (guid, guid);
            yield return (Guid.Empty, Guid.Empty);
            var now1 = DateTime.UtcNow;
            yield return (now1, now1);
            yield return (DateTime.MaxValue, DateTime.MaxValue);
            yield return (DateTime.MinValue, DateTime.MinValue);
            yield return ((DateTime?)null, (DateTime?)null);
            var now2 = DateTimeOffset.UtcNow;
            yield return (now2, now2);
            // TODO FIX yield return (DateTimeOffset.MaxValue, DateTimeOffset.MaxValue);
            // TODO FIX yield return (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            yield return ((DateTimeOffset?)null, (DateTimeOffset?)null);
            yield return (TimeSpan.Zero, TimeSpan.Zero);
            yield return (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            yield return (TimeSpan.FromDays(5555), TimeSpan.FromDays(5555));
            yield return (TimeSpan.MaxValue, TimeSpan.MaxValue);
            yield return (TimeSpan.MinValue, TimeSpan.MinValue);
            yield return ((TimeSpan?)null, (TimeSpan?)null);
            yield return (BigInteger.Zero, BigInteger.Zero);
            yield return (BigInteger.One, BigInteger.One);
            yield return (BigInteger.MinusOne, BigInteger.MinusOne);
            yield return (new BigInteger(ulong.MaxValue) + 1, new BigInteger(ulong.MaxValue) + 1);
            yield return ((BigInteger?)null, (BigInteger?)null);
        }

        [Fact]
        public void SerializerXmlElement()
        {
            var document = new XmlDocument();
            document.LoadXml(@"<note>
<to>Tove</to>
<from>Jani</from>
<heading>Reminder</heading>
<body>Don't forget me this weekend!</body>
</note>");
            var expected = document.DocumentElement;
            var result = Serializer.SerializeToString(expected, SerializeOption.Indented);
            var actual = Serializer.Deserialize<XmlElement>(result);

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializerMatrix1()
        {
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
            var expected = new int[,,]{ { { 1, 2, 3 }, { 4, 5, 6 } },
                                 { { 7, 8, 9 }, { 10, 11, 12 } } };
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
            var result = Serializer.SerializeToString(expected, SerializeOption.Indented);
            var actual = Serializer.Deserialize<int[,,]>(result);

            Assert.NotNull(actual);
            Assert.True(expected.Cast<object>().SequenceEqual(actual!.Cast<object>()));
        }

        [Fact]
        public void SerializerMatrix2()
        {
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
            var expected = new byte[,,] { { { 1, 2, 3 }, { 4, 5, 6 } },
                                 { { 7, 8, 9 }, { 10, 11, 12 } } };
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
            var result = Serializer.SerializeToString(expected);
            var actual = Serializer.Deserialize<byte[,,]>(result);

            Assert.NotNull(actual);
            Assert.True(expected.Cast<object>().SequenceEqual(actual!.Cast<object>()));
        }

        [Fact]
        public void SerializerMatrix3()
        {
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
            var expected = new string[,,] { { { "1", "2", "3" }, { "4", "5", "6" } },
                                 { { "7", "8", "9" }, { "10", "11", "12" } } };
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
            var result = Serializer.SerializeToString(expected);
            var actual = Serializer.Deserialize<string[,,]>(result);

            Assert.NotNull(actual);
            Assert.True(expected.Cast<object>().SequenceEqual(actual!.Cast<object>()));
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerDeserializer(object? o, Type type)
        {
            var result = Serializer.Deserialize(Serializer.SerializeObjectToString(o, type), type);
            Assert.NotNull(result);
            Assert.Equal(o, result);
            Assert.Equal(o?.GetType(), result?.GetType());
        }

        [Theory]
        [MemberData(nameof(GetNulls))]
        public void SerializerDeserializerNullable(Type type)
        {
            var result = Serializer.Deserialize(Serializer.SerializeObjectToString(null, type), type);
            Assert.Null(result);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariant3(object? o, Type type)
        {
            Assert.NotNull(type);
            var result = Serializer.FromArray(o, o, o);
            Assert.NotNull(result);
            Assert.True(result.IsListOfValues);
            Assert.Equal(3, result.Count);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariant2(object? o, Type type)
        {
            Assert.NotNull(type);
            var result = Serializer.FromArray(o, o);
            Assert.NotNull(result);
            Assert.True(result.IsListOfValues);
            Assert.Equal(2, result.Count);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariantToObject(object? o, Type type)
        {
            var expected = type.MakeArrayType();
            var array = Serializer.FromArray(o, o, o).ConvertTo(expected);

            Assert.NotNull(array);
            Assert.Equal(expected, array?.GetType());
        }

        [Fact]
        public void SerializerArrayVariantToObjectWithBytes()
        {
            var expected = typeof(byte).MakeArrayType();
            const byte o = 33;
            var array = Serializer.FromArray(o, o, o).ConvertTo(expected);

            Assert.NotNull(array);
            Assert.Equal(expected, array?.GetType());
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerVariant(object? o, Type type)
        {
            var result = Serializer.FromObject(o).ConvertTo(type);
            Assert.NotNull(result);
            Assert.Equal(o, result);
            Assert.Equal(o?.GetType(), result?.GetType());
        }

        [Theory]
        [MemberData(nameof(GetNulls))]
        public void SerializerVariantNullable(Type type)
        {
            var result = Serializer.FromObject(null).ConvertTo(type);
            Assert.Null(result);
        }

        [Theory]
        [MemberData(nameof(GetVariantValueAndValue))]
        public void SerializerSerializeValueToStringAndCompare(VariantValue? v, object? o)
        {
            var actual = Serializer.SerializeToString(v);
            var expected = Serializer.SerializeToString(o);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetVariantValueAndValue))]
        public void JsonConvertRawAndStringCompare(VariantValue? v, object? o)
        {
            var expected = JsonSerializer.Serialize(o,
                new DefaultJsonSerializer().Settings);
            var actual = Serializer.SerializeToString(v);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetVariantValues))]
        public void SerializerStringParse(VariantValue? v)
        {
            var expected = v;
            var encstr = Serializer.SerializeToString(v);
            var actual = Serializer.Parse(encstr);

            Assert.True(expected?.Equals(actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetVariantValues))]
        public void SerializerFromObject(VariantValue? v)
        {
            var expected = v;
            var actual = Serializer.FromObject(v);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent1()
        {
            var expected = Serializer.FromObject(new
            {
                Test = "Text",
                Locale = "de"
            });
            var actual = Serializer.FromObject(new
            {
                Locale = "de",
                Test = "Text"
            });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent2()
        {
            var expected = Serializer.FromObject(new
            {
                Test = 1,
                LoCale = "de"
            });
            var actual = Serializer.FromObject(new
            {
                Locale = "de",
                TeSt = 1
            });

            Assert.True(expected.Equals(actual));
            Assert.True(expected == actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestDataContractDefaultValuesString()
        {
            var str = Serializer.SerializeToString(new DataContractModelWithDefaultValues());
            Assert.Equal("{}", str);
        }

        [Fact]
        public void TestDataContractDefaultValues()
        {
            var str = Serializer.SerializeToMemory(new DataContractModelWithDefaultValues());
            var result = Serializer.Deserialize<DataContractModelWithDefaultValues>(str.ToArray());
            Assert.Equal(0, result?.Test1);
            Assert.Null(result?.Test2);
            Assert.Null(result?.Test3);
            Assert.Equal(4, result?.Test4);
        }

        [Fact]
        public void TestDataContractModelWithDefaultValues2()
        {
            var str = Serializer.SerializeToString(new DataContractModelWithDefaultValues2());
            Assert.Equal("{}", str);
        }

        [Fact]
        public void TestDataContract()
        {
            var str = Serializer.SerializeToString(new DataContractModel3());
            Assert.Equal("{\"a\":8}", str);
        }

        [Fact]
        public void TestDataContractDefaultValuesAndVariantValueAsNull()
        {
            var str = Serializer.SerializeToMemory(new DataContractModelWithVariantNullValue
            {
                Test1 = 5,
                Test3 = DataContractEnum.All,
                Test4 = 8,
                TestStr = "T"
            });
            var result = Serializer.Deserialize<DataContractModelWithVariantNullValue>(str.ToArray());
            Assert.Equal(5, result?.Test1);
            Assert.Equal(4, result?.Test4);
            Assert.Equal("T", result?.TestStr);
            Assert.Equal(DataContractEnum.All, result?.Test3);
        }

        [Fact]
        public void TestDataContract2()
        {
            var v1 = GetInstanceOfModel();
            var v2 = Serializer.Deserialize<DataContractModel2>(
                Serializer.SerializeToMemory(v1).ToArray());
            Assert.Equal(v1, v2);
        }

        [Fact]
        public void TestDataContract2Enumerable()
        {
            var v1 = GetInstanceOfModel();
            var v2 = Serializer.Deserialize<IEnumerable<DataContractModel2>>(
                Serializer.SerializeToMemory(Enumerable.Repeat(v1, 100)).ToArray());
            Assert.NotNull(v2);
            Assert.All(v2, v => Assert.Equal(v, v1));
        }

        private static readonly JsonSerializerOptions kCaseInsensitive = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public void TestDataContract3()
        {
            var v1 = GetInstanceOfModel();
            var s = JsonSerializer.Serialize(v1, kCaseInsensitive);
            var v2 = Serializer.Deserialize<DataContractModel2>(s);
            Assert.Equal(v1, v2);
        }

        [Fact]
        public void TestDataContract4()
        {
            const string s = "{\"test1\":444,\"test2\":\"sfgasfkadflf\",\"bytes\":[1,2,3,4,5,6,7,8],\"set\":[\"a\",\"b\",\"c\",\"dddd\"],\"roset\":[\"a\",\"b\",\"c\",\"dddd\"],\"Strings\":[\"a\",\"b\",\"c\",\"dddd\"],\"rostrings\":[\"a\",\"b\",\"c\",\"dddd\"],\"stringsOfStrings\":[[\"aa\",\"bg\",\"ca\",\"ddddg\"],[\"a3333\",\"b\",\"c\",\"dddd\"],[\"a\",\"b3333\",\"c\",\"dddd\"],[\"a\",\"b\",\"c3333\",\"dddd\"],[\"a\",\"b\",\"c\",\"dddd3333\"]],\"roStringsOfStrings\":[[\"aa\",\"bg\",\"ca\",\"ddddg\"],[\"a3333\",\"b\",\"c\",\"dddd\"],[\"a\",\"b3333\",\"c\",\"dddd\"],[\"a\",\"b\",\"c3333\",\"dddd\"],[\"a\",\"b\",\"c\",\"dddd3333\"]],\"Dictionary\":{\"test1\":\"3test\",\"test2\":\"2test\",\"test3\":\"4test\",\"test4\":\"6test\"},\"RoDictionary\":{\"test1\":\"3test\",\"test2\":\"2test\",\"test3\":\"4test\",\"test4\":\"6test\"}}";
            var v2 = Serializer.Deserialize<DataContractModel2>(s);
            Assert.Equal(444, v2!.Test1);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(1000)]
        // [InlineData(100000)]
        public async Task TestDataContract2AsyncEnumerableSerializeDeserialize1Async(int count)
        {
            var v1 = GetInstanceOfModel();
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                var va1 = Enumerable.Repeat(v1, count).ToAsyncEnumerable();
                await Serializer.SerializeAsync(stream, va1);

                stream.Position = 0;

                await foreach (var v2 in Serializer.DeserializeStreamAsync<DataContractModel2>(
                    stream).ConfigureAwait(false))
                {
                    Assert.Equal(v1, v2);
                }
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(1000)]
        // [InlineData(100000)]
        public async Task TestDataContract2AsyncEnumerableAsync(int count)
        {
            var v1 = GetInstanceOfModel();
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                var va1 = Enumerable.Repeat(v1, count).ToArray();
                await Serializer.SerializeAsync(stream, va1);

                stream.Position = 0;

                await foreach (var v2 in Serializer.DeserializeStreamAsync<DataContractModel2>(
                    stream).ConfigureAwait(false))
                {
                    Assert.Equal(v1, v2);
                }
            }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        // [InlineData(100000)]
        public async Task TestDataContract2AsyncEnumerableSerializeDeserialize2Async(int count)
        {
            var v1 = GetInstanceOfModel();
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                var va1 = Enumerable.Repeat(v1, count).ToArray();
                await Serializer.SerializeAsync(stream, va1);

                stream.Position = 0;

                await foreach (var v2 in Serializer.DeserializeStreamAsync<DataContractModel2>(
                    stream).ConfigureAwait(false))
                {
                    Assert.Equal(v1, v2);
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public async Task SerializerDeserializerAsync(object? o, Type type)
        {
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                await Serializer.SerializeAsync(stream, o);
                stream.Position = 0;

                var result = await Serializer.DeserializeAsync(stream, type);
                Assert.NotNull(result);
                Assert.Equal(o, result);
                Assert.Equal(o?.GetType(), result?.GetType());
            }
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public async Task SerializerDeserializerAsyncEnumerableAsync(object? o, Type type)
        {
            var v1 = o;
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                var va1 = Enumerable.Repeat(v1, 1000).ToArray();
                await Serializer.SerializeAsync(stream, va1);

                stream.Position = 0;

                await foreach (var v2 in Serializer.DeserializeStreamAsync(
                    stream, type).ConfigureAwait(false))
                {
                    Assert.NotNull(v2);
                    Assert.Equal(v1, v2);
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public async Task SerializerDeserializerAsyncArrayAsync(object? o, Type type)
        {
            if (type == typeof(byte))
            {
                return;
            }
            var v1 = o;
            var stream = new MemoryStream(); await using (stream.ConfigureAwait(false))
            {
                var va1 = Enumerable.Repeat(v1, 100).ToArray();
                await Serializer.SerializeAsync(stream, va1);

                stream.Position = 0;

                var va2 = (Array?)await Serializer.DeserializeAsync(stream,
                    type.MakeArrayType());
                Assert.NotNull(va2);
                foreach (var v2 in va2!)
                {
                    Assert.Equal(v1, v2);
                }
            }
        }

        private static DataContractModel2 GetInstanceOfModel()
        {
            return new DataContractModel2
            {
                Bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                Dictionary = new Dictionary<string, string>
                {
                    ["test1"] = "3test",
                    ["test2"] = "2test",
                    ["test3"] = "4test",
                    ["test4"] = "6test",
                },
                RoDictionary = new Dictionary<string, string>
                {
                    ["test1"] = "3test",
                    ["test2"] = "2test",
                    ["test3"] = "4test",
                    ["test4"] = "6test",
                },
                Strings = new List<string> { "a", "b", "c", "dddd" },
                RoStrings = new List<string> { "a", "b", "c", "dddd" },
                Set = new HashSet<string> { "a", "b", "c", "dddd" },
                RoSet = new HashSet<string> { "a", "b", "c", "dddd" },
                Test1 = 444,
                Test2 = "sfgasfkadflf",
                RoStringsOfStrings = new List<IReadOnlyList<string>> {
                    new List<string> { "aa", "bg", "ca", "ddddg" },
                    new List<string> { "a3333", "b", "c", "dddd" },
                    new List<string> { "a", "b3333", "c", "dddd" },
                    new List<string> { "a", "b", "c3333", "dddd" },
                    new List<string> { "a", "b", "c", "dddd3333" },
                },
                StringsOfStrings = new List<IList<string>> {
                    new List<string> { "aa", "bg", "ca", "ddddg" },
                    new List<string> { "a3333", "b", "c", "dddd" },
                    new List<string> { "a", "b3333", "c", "dddd" },
                    new List<string> { "a", "b", "c3333", "dddd" },
                    new List<string> { "a", "b", "c", "dddd3333" },
                }
            };
        }

        [Fact]
        public void TestDataContractEnum1()
        {
            var str = Serializer.SerializeToString(DataContractEnum.Test1 | DataContractEnum.Test2);
            Assert.Equal("\"tst1, test2\"", str);
            var result = Serializer.Deserialize<DataContractEnum>(str);
            Assert.Equal(DataContractEnum.Test1 | DataContractEnum.Test2, result);
        }

        [Fact]
        public void TestDataContractEnum2()
        {
            var str = Serializer.SerializeToString(DataContractEnum.All);
            Assert.Equal("\"all\"", str);
            var result = Serializer.Deserialize<DataContractEnum>(str);
            Assert.Equal(DataContractEnum.All, result);
        }

        [Fact]
        public void SerializerFromObjectContainerToContainerWithObject()
        {
            var expected = new TestContainer
            {
                Value = Serializer.FromObject(new
                {
                    Test = "Text",
                    Locale = "de"
                })
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetVariantValues))]
        public void SerializerFromObjectContainerToContainer(VariantValue? v)
        {
            var expected = new TestContainer
            {
                Value = v
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerFromObjectContainerToContainerWithSerializedVariant(object? o, Type type)
        {
            Assert.NotNull(type);
            var expected = new TestContainer
            {
                Value = Serializer.FromObject(o)
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerFromObjectContainerToContainerWithArray(object? o, Type type)
        {
            Assert.NotNull(type);
            var expected = new TestContainer
            {
                Value = Serializer.FromArray(o, o, o)
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        [Fact]
        public void SerializerFromObjectContainerToContainerWithStringArray()
        {
            var expected = new TestContainer
            {
                Value = Serializer.FromArray("", "", "")
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        [Fact]
        public void SerializerFromObjectContainerToContainerWithDateTimeOffset()
        {
            var expected = new TestContainer
            {
                Value = DateTimeOffset.UtcNow
            };
            var tmp = Serializer.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromComplexObjectAndGetByPath()
        {
            var o = Serializer.FromObject(new
            {
                Test = 0,
                Path1 = new
                {
                    Test = 1,
                    a = new[] { 1, 2, 3, 4, 5 },
                    Path2 = new
                    {
                        Test = 2,
                        a = new[] { 1, 2, 3, 4, 5 },
                        Path3 = new
                        {
                            Test = 3,
                            a = new[] { 1, 2, 3, 4, 5 },
                            Path4 = new
                            {
                                Test = 4,
                                a = new[] { 1, 2, 3, 4, 5 }
                            }
                        }
                    }
                },
                LoCale = "de"
            });
            var value = o.GetByPath("Path1.Test");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.Path2.Test");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.Path2.Path3.Test");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.Path4.Test");
            Assert.Equal(4, value);

            value = o.GetByPath("path1.Test");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.path2.Test");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.PAth2.PaTh3.TEST");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.Path4.test");
            Assert.Equal(4, value);

            value = o.GetByPath("Path1.a");
            Assert.True(value.IsListOfValues);
            Assert.Equal(5, value.Count);

            value = o.GetByPath("Path1.a[0]");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.path2.a[1]");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.PAth2.PaTh3.a[2]");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.Path4.a[3]");
            Assert.Equal(4, value);
        }

        [Fact]
        public void SerializeFromComplexObjectAndGetByPath1()
        {
            var o = Serializer.FromObject(new
            {
                Test = 0,
                Path1 = new
                {
                    Test = 1,
                    Path2 = new
                    {
                        Test = 2,
                        Path3 = new
                        {
                            Test = 3,
                            Path4 = new
                            {
                                Test = 4,
                            }
                        }
                    }
                },
                LoCale = "de"
            });
            var value = o.GetByPath("Path1.Test");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.Path2.Test");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.Path2.Path3.Test");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.Path4.Test");
            Assert.Equal(4, value);

            value = o.GetByPath("path1.Test");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.path2.Test");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.PAth2.PaTh3.TEST");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.Path4.test");
            Assert.Equal(4, value);

            value = o.GetByPath("path1.Tst");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.path2.Tst");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.PAth2.PaTh3.TST");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.Path2.Path3.Path4.tst");
            Assert.True(value.IsNull());
        }

        [Fact]
        public void SerializeFromComplexObjectAndGetByPath2()
        {
            var o = Serializer.FromObject(new
            {
                Test = 0,
                Path1 = new
                {
                    Test = 1,
                    a = new[] { 1, 2, 3, 4, 5 },
                    Path2 = new
                    {
                        Test = 2,
                        a = new[] { 1, 2, 3, 4, 5 },
                        Path3 = new
                        {
                            Test = 3,
                            a = new[] { 1, 2, 3, 4, 5 },
                            Path4 = new
                            {
                                Test = 4,
                                a = new[] { 1, 2, 3, 4, 5 }
                            }
                        }
                    }
                },
                LoCale = "de"
            });
            var value = o.GetByPath("Path1.a");
            Assert.True(value.IsListOfValues);
            Assert.Equal(5, value.Count);

            value = o.GetByPath("Path1.A[0]");
            Assert.Equal(1, value);
            value = o.GetByPath("Path1.path2.a[1]");
            Assert.Equal(2, value);
            value = o.GetByPath("Path1.PAth2.PaTh3.a[2]");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.Path2.Path3.PATH4.a[3]");
            Assert.Equal(4, value);

            value = o.GetByPath("Path1.B[0]");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.path2.b[1]");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.PAth2.PaTh3.b[2]");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.Path2.Path3.PATH4.b[3]");
            Assert.True(value.IsNull());
        }

        [Fact]
        public void SerializeFromComplexObjectAndGetByPath3()
        {
            var o = Serializer.FromObject(new
            {
                Test = 0,
                Path1 = new
                {
                    a = new object[] {
                        new {
                            Test = 3,
                            a = new[] { 1, 2, 3, 4, 5 },
                            Path2 = new {
                                Test = 2,
                                a = new[] { 1, 2, 3, 4, 5 }
                            }
                        },
                        new {
                            Test = 3,
                            a = new[] { 1, 2, 3, 4, 5 },
                            Path3 = new {
                                Test = 3,
                                a = new[] { 1, 2, 3, 4, 5 }
                            }
                        },
                        new {
                            Test = 3,
                            a = new[] { 1, 2, 3, 4, 5 },
                            Path4 = new {
                                Test = 4,
                                a = new[] { 1, 2, 3, 4, 5 }
                            }
                        }
                    }
                },
                LoCale = "de"
            });
            var value = o.GetByPath("Path1.a");
            Assert.True(value.IsListOfValues);
            Assert.Equal(3, value.Count);

            value = o.GetByPath("Path1.a[0]");
            Assert.True(value.IsObject);

            value = o.GetByPath("Path1.a[1].Test");
            Assert.Equal(3, value);
            value = o.GetByPath("Path1.a[1].Path3.Test");
            Assert.Equal(3, value);

            value = o.GetByPath("Path1.a[2].Path4.a");
            Assert.True(value.IsListOfValues);
            Assert.Equal(5, value.Count);
            value = o.GetByPath("Path1.a[2].Path4.a[2]");
            Assert.Equal(3, value);

            value = o.GetByPath("Path1.a[4]");
            Assert.True(value.IsNull());
            value = o.GetByPath("Path1.a[4].Test");
            Assert.True(value.IsNull());
        }

        [Fact]
        public void SerializeEndpointString1()
        {
            const string? expected = "Endpoint";
            var json = Serializer.SerializeToString(expected);
            var actual = Serializer.Parse(json);
            VariantValue expected1 = "Endpoint";

            Assert.True(actual == expected);
            Assert.Equal(expected, actual);
            Assert.Equal(expected1, actual);
            Assert.Equal(expected, actual.ConvertTo<string>());
            Assert.Equal(expected, expected1.ConvertTo<string>());
        }

        [Fact]
        public void SerializeEndpointString2()
        {
            VariantValue expected = "Endpoint";
            var json = Serializer.SerializeToString(expected);
            var actual = Serializer.Parse(json);
            const string? expected1 = "Endpoint";

            Assert.True(actual.Equals(expected));
            Assert.Equal(expected, actual);
            Assert.Equal(expected1, actual);
            Assert.Equal(expected, actual.ConvertTo<string>());
        }

        [Fact]
        public void AssignArrayTest1()
        {
            var array = new[] { 1, 2, 3, 4, 5 };
            var json = Serializer.SerializeObjectToString(array);
            var variant = Serializer.Parse(json);
            variant[2].AssignValue(0);
            Assert.Equal(0, variant[2]);
            Assert.Equal("[1,2,0,4,5]", variant.ToJson());
        }

        [Fact]
        public void AssignArrayTest2()
        {
            var array = new[] { 1, 2, 3, 4, 5 };
            var json = Serializer.SerializeObjectToString(array);
            var variant = Serializer.Parse(json);
            variant[2].AssignValue("x");
            Assert.Equal("x", variant[2]);
            Assert.Equal("[1,2,\"x\",4,5]", variant.ToJson());
        }

        [Fact]
        public void AssignArrayTest3()
        {
            var array = new[] { 1, 2, 3, 4, 5 };
            var json = Serializer.SerializeObjectToString(array);
            var variant = Serializer.Parse(json);
            variant[2].AssignValue(null);
            Assert.True(variant[2].IsNull);
            Assert.Equal("[1,2,null,4,5]", variant.ToJson());
        }

        [Fact]
        public void AssignObjectTest1()
        {
            var o = new
            {
                a = 1,
                b = "b",
                c = new
                {
                    test = "test"
                }
            };
            var json = Serializer.SerializeObjectToString(o);
            var variant = Serializer.Parse(json);
            variant["Id"].AssignValue("idField");
            Assert.Equal("idField", variant["Id"]);
            var updated = variant.ToJson();
            Assert.Equal("{\"a\":1,\"b\":\"b\",\"c\":{\"test\":\"test\"},\"Id\":\"idField\"}", variant.ToJson());
        }

        [Fact]
        public void AssignObjectTest2()
        {
            var o = new
            {
                a = 1,
                b = "b"
            };
            var json = Serializer.SerializeObjectToString(o);
            var variant = Serializer.Parse(json);
            variant["b"].AssignValue(999);
            Assert.Equal(999, variant["b"]);
        }

        [Fact]
        public void AssignObjectTest3()
        {
            var o = new
            {
                a = 1,
                b = "b"
            };
            var json = Serializer.SerializeObjectToString(o);
            var variant = Serializer.Parse(json);
            variant["b"].AssignValue(null);
            Assert.True(variant["b"].IsNull);
        }

        public static IEnumerable<object[]> GetNulls()
        {
            return GetStrings()
                .Select(v => v.Item2.GetType())
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v =>
                    typeof(Nullable<>).MakeGenericType(v.Item2!.GetType())))
                .Distinct()
                .Select(t => new object[] { t });
        }

        public static IEnumerable<object[]> GetScalars()
        {
            return GetStrings()
                .Select(v => new object[] { v.Item2, v.Item2.GetType() })
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { v.Item2!, v.Item2!.GetType() })
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { v.Item2!,
                    typeof(Nullable<>).MakeGenericType(v.Item2!.GetType()) })));
        }

        public static IEnumerable<object[]> GetFilledArrays()
        {
            return GetStrings()
                .Select(v => new object[] { CreateArray(v.Item2, v.Item2.GetType(), 10),
                    v.Item2.GetType().MakeArrayType()})
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { CreateArray(v.Item2, v.Item2!.GetType(), 10),
                    v.Item2.GetType().MakeArrayType() }));
        }

        public static IEnumerable<object[]> GetEmptyArrays()
        {
            return GetStrings()
                .Select(v => new object[] { CreateArray(null, v.Item2.GetType(), 10),
                    v.Item2.GetType().MakeArrayType()})
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { CreateArray(null, v.Item2!.GetType(), 10),
                    v.Item2.GetType().MakeArrayType() }));
        }

        public static IEnumerable<object[]> GetVariantValues()
        {
            return GetValues()
                .Select(v => new object[] { v.Item1 })
                .Concat(GetStrings()
                .Select(v => new object[] { v.Item1 }));
        }

        public static IEnumerable<object?[]> GetVariantValueAndValue()
        {
            return GetStrings()
                .Select(v => new object?[] { v.Item1, v.Item2 })
                .Concat(GetValues()
                .Select(v => new object?[] { v.Item1, v.Item2 }));
        }

        private static Array CreateArray(object? value, Type type, int size)
        {
            var array = Array.CreateInstance(type, size);
            if (value != null)
            {
                for (var i = 0; i < size; i++)
                {
                    array.SetValue(value, i);
                }
            }
            return array;
        }
    }
}
