// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc.Servers
{
    using Furly.Extensions.Rpc.Runtime;
    using Furly.Extensions.Configuration;
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class FileSystemRpcServerTests
    {
        [Fact]
        public async Task FileSystemRpcServerTest1()
        {
            const string requestContent = """
POST method HTTP/1.1
Content-Type: application/json

{"key":"value"}
""";
            const string expectedResponse = """
POST method HTTP/1.1
200

{"a":"b"}


""";
            // Configure input and output paths
            var input = Path.Combine(Path.GetTempPath(), "input");
            var output = Path.Combine(Path.GetTempPath(), "output");
            const string reqFileName = "request";
            var options = new OptionsMock<FileSystemOptions>();
            options.Value.RequestPath = input;
            options.Value.ResponsePath = output;

            var provider = new Mock<IFileProvider>();
            var reqPath = new Mock<IDirectoryContents>();
            var resPath = new Mock<IDirectoryContents>();

            var reqFile = new Mock<IFileInfo>();
            reqFile.SetupGet(file => file.Exists).Returns(true);
            reqFile.SetupGet(file => file.Name).Returns(reqFileName + ".http");
            reqFile.Setup(file => file.CreateReadStream())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(requestContent))); // Return file content

            var resFile = new Mock<IFileInfoEx>();
            var outputStream = new MemoryStream();
            resFile.SetupGet(file => file.IsWritable).Returns(true);
            resFile.SetupGet(file => file.Exists).Returns(false);
            resFile.Setup(file => file.CreateWriteStream())
                .Returns(outputStream);
            provider.Setup(provider => provider.GetFileInfo(
                It.Is<string>(d => d == Path.Combine(output, reqFileName + ".resp"))))
                .Returns(resFile.Object);

            provider.Setup(provider => provider.GetDirectoryContents(It.Is<string>(d => d == input)))
                .Returns(reqPath.Object);
            provider.Setup(provider => provider.GetDirectoryContents(It.Is<string>(d => d == output)))
                .Returns(resPath.Object);
            provider.Setup(provider => provider.Watch(It.IsAny<string>()))
                .Returns(new Mock<IChangeToken>().Object);

            reqPath.Setup(dir => dir.GetEnumerator())
                .Returns(reqFile.Object.YieldReturn().GetEnumerator()); // One file there
            resPath.Setup(dir => dir.GetEnumerator())
                .Returns(Enumerable.Empty<IFileInfo>().GetEnumerator()); // No files there yet

            await using (var server = new FileSystemRpcServer(provider.Object,
                new Mock<ISerializer>().Object, options, Logging.Log.Console<FileSystemRpcServer>()))
            {
                var handler = new FuncDelegate("test", (method, payload, contentType, ct) =>
                {
                    Assert.Equal("application/json", contentType);
                    Assert.Equal("method", method);
                    Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(payload.ToArray()));

                    return Encoding.UTF8.GetBytes("{\"a\":\"b\"}");
                });

                await using var r = await server.ConnectAsync(handler);
                server.Start(); // Processing files

                await Task.Delay(1000); // Wait for processing
            }
            Assert.Equal(expectedResponse, Encoding.UTF8.GetString(outputStream.ToArray()));
            provider.Verify();
        }

        [Fact]
        public async Task FileSystemRpcServerTest2()
        {
            // Configure input and output paths
            var input = Path.Combine(Path.GetTempPath(), "a");
            var output = Path.Combine(Path.GetTempPath(), "b");
            const string reqFileName = "request";
            var options = new OptionsMock<FileSystemOptions>();
            options.Value.RequestPath = input;
            options.Value.ResponsePath = output;

            var provider = new Mock<IFileProvider>();
            var reqPath = new Mock<IDirectoryContents>();
            var resPath = new Mock<IDirectoryContents>();

            var reqFile = new Mock<IFileInfo>();
            reqFile.SetupGet(file => file.Name).Returns(reqFileName + ".http");

            var resFile = new Mock<IFileInfo>();
            resFile.SetupGet(file => file.Name).Returns(reqFileName + ".resp");

            provider.Setup(provider => provider.GetDirectoryContents(It.Is<string>(d => d == input)))
                .Returns(reqPath.Object);
            provider.Setup(provider => provider.GetDirectoryContents(It.Is<string>(d => d == output)))
                .Returns(resPath.Object);
            provider.Setup(provider => provider.Watch(It.IsAny<string>()))
                .Returns(new Mock<IChangeToken>().Object);

            reqPath.Setup(dir => dir.GetEnumerator())
                .Returns(reqFile.Object.YieldReturn().GetEnumerator()); // One file there
            resPath.Setup(dir => dir.GetEnumerator())
                .Returns(resFile.Object.YieldReturn().GetEnumerator()); // One file there

            var called = false;
            await using (var server = new FileSystemRpcServer(provider.Object,
                new Mock<ISerializer>().Object, options, Logging.Log.Console<FileSystemRpcServer>()))
            {
                var handler = new FuncDelegate("test", (method, payload, contentType, ct) =>
                {
                    called = true; // Should never be here
                    Assert.False(true);
                    return Encoding.UTF8.GetBytes("{\"a\":\"b\"}");
                });
                await using var r = await server.ConnectAsync(handler);
                server.Start(); // Processing files
                await Task.Delay(1000); // Wait for processing
            }
            Assert.False(called);
            provider.Verify();
        }
    }
}
