// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc
{
    using Microsoft.Extensions.FileProviders;
    using System.IO;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class PhysicalFileProviderTests
    {
        [Fact]
        public async Task TestFileProviderWorksAsExpected()
        {
            var f1full = Path.GetTempFileName();
            var f2full = Path.GetTempFileName();

            var path1 = Path.GetDirectoryName(f1full);
            var path2 = Path.GetDirectoryName(f2full);
            Assert.True(Directory.Exists(path1));
            Assert.True(Directory.Exists(path2));

            using var provider1 = new PhysicalFileProvider(path1);
            using var provider2 = new PhysicalFileProvider(path2);

            var file1 = Path.GetFileName(f1full);
            var file2 = Path.GetFileName(f2full);
            await using (var s = File.Create(f1full))
            {
                s.WriteByte(3);
            }
            await using (var s = File.CreateText(f2full))
            {
                await s.WriteLineAsync("test file");
            }

            Assert.True(File.Exists(f1full));
            Assert.True(File.Exists(f2full));

            var finfo1 = new FileInfo(f1full);
            Assert.True(finfo1.Exists);
            var finfo2 = new FileInfo(f2full);
            Assert.True(finfo2.Exists);

            var fi1 = provider1.GetFileInfo(file1);
            Assert.NotNull(fi1);
            var fi2 = provider2.GetFileInfo(file2);
            Assert.NotNull(fi2);

            Assert.Equal(file1, fi1.Name);
            Assert.Equal(file2, fi2.Name);
            Assert.Equal(f1full, fi1.PhysicalPath);
            Assert.Equal(f2full, fi2.PhysicalPath);

            var dt1 = File.GetLastWriteTimeUtc(f1full);
            Assert.Equal(dt1, fi1.LastModified);

            Assert.NotEqual(fi1, fi2);
            Assert.True(fi1.Exists);
            Assert.True(fi2.Exists);
            Assert.NotEqual(fi1.LastModified, fi2.LastModified);

            File.SetLastWriteTimeUtc(f2full, fi1.LastModified.DateTime);
            fi2 = provider2.GetFileInfo(file2);
            var dt2 = File.GetLastWriteTimeUtc(f2full);
            Assert.Equal(dt2, dt1);
            Assert.Equal(dt2, fi1.LastModified);

            Assert.Equal(dt2, fi2.LastModified);
            Assert.Equal(fi1.LastModified, fi2.LastModified);
        }
    }
}
