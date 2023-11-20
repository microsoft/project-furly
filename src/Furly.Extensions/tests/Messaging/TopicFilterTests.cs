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
