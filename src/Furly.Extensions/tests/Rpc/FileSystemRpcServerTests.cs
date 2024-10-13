// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc
{
    using Furly.Extensions.Rpc.Servers;
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

            var options = new OptionsMock<FileSystemRpcServerOptions>();
            options.Value.RequestFilePath = Path.Combine(input, "request");
            options.Value.ResponseFilePath = Path.Combine(output, "response");

            var reqProvider = new Mock<IFileProvider>();
            var resProvider = new Mock<IFileProvider>();
            var factory = new Mock<IFileProviderFactory>();
            factory.Setup(factory => factory.Create(It.Is<string>(d => d == input))).Returns(reqProvider.Object);
            factory.Setup(factory => factory.Create(It.Is<string>(d => d == output))).Returns(resProvider.Object);

            var dt = DateTimeOffset.UtcNow;
            var reqFile = new Mock<IFileInfo>();
            reqFile.SetupGet(file => file.Exists).Returns(true);
            reqFile.SetupGet(file => file.Name).Returns("request");
            reqFile.SetupGet(file => file.LastModified).Returns(dt);
            reqFile.Setup(file => file.CreateReadStream())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(requestContent))); // Return file content
            reqProvider.Setup(provider => provider.Watch(It.Is<string>(d => d == "request")))
                .Returns(new Mock<IChangeToken>().Object);
            reqProvider.Setup(provider => provider.GetFileInfo(It.Is<string>(d => d == "request")))
                .Returns(reqFile.Object);

            var resFile = new Mock<IFileInfoEx>();
            var outputStream = new MemoryStream();
            resFile.SetupGet(file => file.IsWritable).Returns(true);
            resFile.SetupGet(file => file.Exists).Returns(false);
            resFile.Setup(file => file.SetLastModified(It.Is<DateTimeOffset>(d => d == dt)))
                .Verifiable(Times.Once);
            resFile.Setup(file => file.CreateWriteStream())
                .Returns(outputStream);
            resProvider.Setup(provider => provider.GetFileInfo(It.Is<string>(d => d == "response")))
                .Returns(resFile.Object);

            await using (var server = new FileSystemRpcServer(factory.Object,
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
            factory.Verify();
        }

        [Fact]
        public async Task FileSystemRpcServerTest2()
        {
            // Configure input and output paths
            var input = Path.Combine(Path.GetTempPath(), "a");
            var output = Path.Combine(Path.GetTempPath(), "b");
            var options = new OptionsMock<FileSystemRpcServerOptions>();
            options.Value.RequestFilePath = Path.Combine(input, "request");
            options.Value.ResponseFilePath = Path.Combine(output, "response");

            var reqProvider = new Mock<IFileProvider>();
            var resProvider = new Mock<IFileProvider>();
            var factory = new Mock<IFileProviderFactory>();
            factory.Setup(factory => factory.Create(It.Is<string>(d => d == input))).Returns(reqProvider.Object);
            factory.Setup(factory => factory.Create(It.Is<string>(d => d == output))).Returns(resProvider.Object);

            var dt = DateTimeOffset.UtcNow;
            var reqFile = new Mock<IFileInfo>();
            reqFile.SetupGet(file => file.Exists).Returns(true);
            reqFile.SetupGet(file => file.LastModified).Returns(dt);
            reqProvider.Setup(provider => provider.Watch(It.Is<string>(d => d == "request")))
                .Returns(new Mock<IChangeToken>().Object);
            reqProvider.Setup(provider => provider.GetFileInfo(It.Is<string>(d => d == "request")))
                .Returns(reqFile.Object);

            var resFile = new Mock<IFileInfoEx>();
            var outputStream = new MemoryStream();
            resFile.SetupGet(file => file.Exists).Returns(true);
            resFile.SetupGet(file => file.LastModified).Returns(dt);
            resProvider.Setup(provider => provider.GetFileInfo(It.Is<string>(d => d == "response")))
                .Returns(resFile.Object);

            var called = false;
            await using (var server = new FileSystemRpcServer(factory.Object,
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
            factory.Verify();
        }
    }
}
