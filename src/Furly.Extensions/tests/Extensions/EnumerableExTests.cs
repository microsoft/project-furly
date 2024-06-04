// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Collections.Generic
{
    using System.Linq;
    using System.Text;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class EnumerableExTests
    {
        [Fact]
        public void ContinueWithTest1()
        {
            var result = 1.YieldReturn().ContinueWith(2).Skip(1).Take(10);
            Assert.All(result, element => Assert.Equal(2, element));
        }
        [Fact]
        public void ContinueWithTest2()
        {
            var result = ((IEnumerable<int>?)null).ContinueWith(2).Take(10);
            Assert.All(result, element => Assert.Equal(2, element));
        }

        [Fact]
        public void ZipSingleWithEmptyEnumerationTest1()
        {
            var result = 1.YieldReturn().Zip(Enumerable.Empty<uint>(), 2u);
            var element = Assert.Single(result);
            Assert.Equal(2u, element.Item2);
        }

        [Fact]
        public void ZipArrayWithEmptyEnumerationTest1()
        {
            var result = new[] { 1, 2, 3, 4 }.Zip(Enumerable.Empty<uint>(), 2u);
            Assert.All(result, element => Assert.Equal(2u, element.Item2));
            Assert.Equal(4, result.Count());
        }

        [Fact]
        public void ZipArrayWithEmptyEnumerationTest2()
        {
            var result = new[] { 1, 2, 3, 4 }.Zip(Enumerable.Empty<object>(), default);
            Assert.All(result, element => Assert.Null(element.Item2));
            Assert.Equal(4, result.Count());
        }

        [Fact]
        public void ZipArrayWithLargerEnumerationTest()
        {
            var result = new[] { -1, -2, -3, -4 }.Zip([-1, -2, -3, -4, -5], 76);
            Assert.All(result, element => Assert.Equal(element.Item1, element.Item2));
            Assert.Equal(4, result.Count());
        }

        [Fact]
        public void ZipEmptyWithEnumerationTest()
        {
            var result = Enumerable.Empty<DateTime>().Zip([-1], 76);
            Assert.Empty(result);
        }

        [Fact]
        public void ZipNullWithEnumerationTest()
        {
            var result = ((IEnumerable<DateTime>?)null).Zip([-1], 76);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        public void Batch0LengthEnumerable(int batchsize)
        {
            var batch = Enumerable.Empty<uint>().Batch(batchsize);
            Assert.Empty(batch);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        public void Batch1LengthEnumerable(int batchsize)
        {
            var batch = "".YieldReturn().Batch(batchsize);
            Assert.Single(Assert.Single(batch));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        public void Batch5LengthEnumerable(int batchsize)
        {
            var batch = new uint[5].Batch(batchsize);
            Assert.Single(batch);
        }

        [Theory]
        [InlineData(6, 5, 2)]
        [InlineData(5, 5, 1)]
        [InlineData(99, 5, 20)]
        [InlineData(99, 1, 99)]
        [InlineData(99, 10000, 1)]
        public void Batch27in5LengthEnumerable(int count, int batchsize, int expected)
        {
            var batch = new uint[count].Batch(batchsize);
            Assert.Equal(expected, batch.Count());
        }

        [Fact]
        public void ThrowWithBadBatchSizeLengths()
        {
            Assert.ThrowsAny<ArgumentException>(() => Enumerable.Empty<uint>().Batch(-1));
            Assert.ThrowsAny<ArgumentException>(() => Enumerable.Empty<uint>().Batch(0));
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenListSubjectNull()
        {
            List<string>? test1 = null;
            var test2 = new List<string> { "serf", "sated" };

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenListObjectNull()
        {
            var test1 = new List<string> { "serf", "sated" };
            List<string>? test2 = null;

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsWReturnsTrueWhenBothListNull()
        {
            List<string>? test1 = null;
            List<string>? test2 = null;

            var result = test1.SequenceEqualsSafe(test2);
            Assert.True(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenEnumerableSubjectNull()
        {
            IEnumerable<string>? test1 = null;
            IEnumerable<string> test2 = new List<string> { "serf", "sated" };

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenEnumerableObjectNull()
        {
            IEnumerable<string> test1 = new List<string> { "serf", "sated" };
            IEnumerable<string>? test2 = null;

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsWReturnsTrueWhenBothEnumerableNull()
        {
            IEnumerable<string>? test1 = null;
            IEnumerable<string>? test2 = null;

            var result = test1.SequenceEqualsSafe(test2);
            Assert.True(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenBufferSubjectNull()
        {
            byte[]? test1 = null;
            var test2 = Encoding.UTF8.GetBytes("testtesttesttest");

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenBufferObjectNull()
        {
            var test1 = Encoding.UTF8.GetBytes("testtesttesttest");
            byte[]? test2 = null;

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsTrueWhenSequenceSame()
        {
            var test1 = new List<string> { "serf", "sated" };
            var test2 = new List<string> { "serf", "sated" };

            var result = test1.SequenceEqualsSafe(test2);
            Assert.True(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenSequenceNotSame1()
        {
            var test1 = new List<string> { "serf", "sated" };
            var test2 = new List<string> { "serf", "sated", "data" };

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenSequenceNotSame2()
        {
            var test1 = new List<string> { "serf", "sated" };
            var test2 = new List<string> { "sated", "serf" };

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }

        [Fact]
        public void SequenceEqualsReturnsTrueWhenBufferSame()
        {
            var test1 = Encoding.UTF8.GetBytes("testtesttesttest");
            var test2 = Encoding.UTF8.GetBytes("testtesttesttest");

            var result = test1.SequenceEqualsSafe(test2);
            Assert.True(result);
        }

        [Fact]
        public void SequenceEqualsReturnsFalseWhenBufferNotSame()
        {
            var test1 = Encoding.UTF8.GetBytes("testtesttesttest");
            var test2 = Encoding.UTF8.GetBytes("testtesttesttesx");

            var result = test1.SequenceEqualsSafe(test2);
            Assert.False(result);
        }
    }
}
