// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class AssemblyExTests
    {
        [Fact]
        public void GetFileVersion()
        {
            var v = GetType().Assembly.GetReleaseVersion().ToString();
            Assert.False(string.IsNullOrEmpty(v));
        }
    }
}
