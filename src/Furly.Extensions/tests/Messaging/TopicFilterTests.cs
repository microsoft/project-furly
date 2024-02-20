// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class TopicFilterTests
    {
        [Fact]
        public void EscapeTests()
        {
            Assert.Equal("\\x2b\\x23", TopicFilter.Escape("+#"));
            Assert.Equal("\\x5c\\x5c", TopicFilter.Escape("\\\\"));
            Assert.Equal("abac acab", TopicFilter.Escape("abac acab"));
            Assert.Equal("ab", TopicFilter.Escape("ab"));
            Assert.Equal("    ", TopicFilter.Escape("    "));
            Assert.Equal("", TopicFilter.Escape(""));
            Assert.Equal("\\x2fa\\x2f\\x2f", TopicFilter.Escape("/a//"));
            Assert.Equal("\\x2fa\\x2fa\\x2f", TopicFilter.Escape("/a/a/"));
            Assert.Equal("\\x2b\\x2fa\\x2f\\x23bcde", TopicFilter.Escape("+/a/#bcde"));
            Assert.Equal("\ntest\\x5c", TopicFilter.Escape("\ntest\\"));
        }

        [Fact]
        public void EmptyStringIsValid()
        {
            Assert.True(TopicFilter.IsValid(""));
        }

        [Fact]
        public void SampleStringIsValid()
        {
            Assert.True(TopicFilter.IsValid("test"));
            Assert.True(TopicFilter.IsValid("/test"));
            Assert.True(TopicFilter.IsValid("/test/"));
            Assert.True(TopicFilter.IsValid("test/test"));
            Assert.True(TopicFilter.IsValid("/test/test"));
        }

        [Fact]
        public void FilterWithSingleIsValid()
        {
            Assert.True(TopicFilter.IsValid("test/+/"));
            Assert.True(TopicFilter.IsValid("/test/+/+/"));
            Assert.True(TopicFilter.IsValid("/test/+"));
            Assert.True(TopicFilter.IsValid("test/+/test"));
            Assert.True(TopicFilter.IsValid("+/test/test"));
            Assert.True(TopicFilter.IsValid("/+/test/test"));
            Assert.True(TopicFilter.IsValid("+"));
        }

        [Fact]
        public void FilterWithMultipleIsValid()
        {
            Assert.True(TopicFilter.IsValid("test/#"));
            Assert.True(TopicFilter.IsValid("/test/#"));
        }

        [Fact]
        public void FilterWithMultipleAndSingleIsValid()
        {
            Assert.True(TopicFilter.IsValid("test/+/#"));
            Assert.True(TopicFilter.IsValid("/test/+/test/#"));
            Assert.True(TopicFilter.IsValid("+/test/#"));
        }

        [Fact]
        public void NullFilterIsInvalid()
        {
            Assert.False(TopicFilter.IsValid(null));
        }

        [Fact]
        public void FilterWithInvalidSingleIsInvalid()
        {
            Assert.False(TopicFilter.IsValid("test+"));
            Assert.False(TopicFilter.IsValid("test/t+/"));
            Assert.False(TopicFilter.IsValid("test/+t"));
            Assert.False(TopicFilter.IsValid("+t"));
            Assert.False(TopicFilter.IsValid("test/+t"));
        }
    }
}
