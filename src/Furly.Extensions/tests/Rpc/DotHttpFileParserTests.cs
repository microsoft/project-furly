// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc
{
    using Furly.Extensions.Rpc.Servers;
    using Furly.Extensions.Storage;
    using Microsoft.Extensions.FileProviders;
    using Moq;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class DotHttpFileParserTests
    {
        [Fact]
        public async Task ParseGetMethodTestWithoutPayload()
        {
            const string req = "GET method HTTP/1.1";

            var res = req + Environment.NewLine + "200" + Environment.NewLine + Environment.NewLine;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Empty(h);
                Assert.Equal(0, r.Length);
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParseGetMethodWithUriTestWithoutPayload()
        {
            const string req = "GET http://localhost:8080/test HTTP/1.1";

            var res = req + Environment.NewLine + "200" + Environment.NewLine + Environment.NewLine;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("GET", method.String);
                Assert.Equal(new Uri("http://localhost:8080/test"), method.Uri);
                Assert.Equal("HTTP/1.1", method.ProtocolVersion);
                Assert.Empty(h);
                Assert.Equal(0, r.Length);
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParsePostWithInputFileTest()
        {
            const string req = """
// @no-redirect
POST method
Content-Type: application/json

< ./input.json
###
""";
            var res = "POST method" + Environment.NewLine + "200"
                + Environment.NewLine + Environment.NewLine
                + "###"
                + Environment.NewLine + Environment.NewLine;

            var provider = new Mock<IFileProvider>();
            var file = new Mock<IFileInfo>();
            provider.Setup(provider => provider.GetFileInfo(It.IsAny<string>()))
                .Returns(file.Object);
            file.SetupGet(file => file.Exists).Returns(true);
            file.Setup(file => file.CreateReadStream())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"key\":\"value\"}")));

            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal(15, r.Length);
                Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(r.Span));
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            }, null, provider.Object, default);
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParsePostMethodWithJsonPayloadTest()
        {
            const string req = """
POST method HTTP/1.1
Content-Type: application/json

{"key":"value"}

###
""";
            var res = "POST method HTTP/1.1" + Environment.NewLine + "200"
                + Environment.NewLine + Environment.NewLine
                + "###"
                + Environment.NewLine + Environment.NewLine;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal(15, r.Length);
                Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(r.Span));
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParsePostMethodWithJsonPayloadNoLogTest()
        {
            const string req = """
// @no-log
POST method HTTP/1.1
Content-Type: application/json

{"key":"value"}

###
""";
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal(15, r.Length);
                Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(r.Span));
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task ParseMultiMethodWithJsonPayloadTest()
        {
            const string req = """
POST add
Content-Type: application/json

{"key":"value"}
###

GET first
###
GET second


###
""";
            var res = "POST add" + Environment.NewLine + "200"
                + Environment.NewLine + Environment.NewLine
                + "###"
                + Environment.NewLine + Environment.NewLine
                + "GET first" + Environment.NewLine + "200"
                + Environment.NewLine + Environment.NewLine
                + "###"
                + Environment.NewLine + Environment.NewLine
                + "GET second" + Environment.NewLine + "200"
                + Environment.NewLine + Environment.NewLine
                + "###"
                + Environment.NewLine + Environment.NewLine;

            var methods = new[] { "add", "first", "second" };
            var counter = 0;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal(methods[counter], method.String);
                counter++;
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(3, counter);
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParseMultiMethodWithJsonPayloadStopsOnErrorTest()
        {
            const string req = """
# @no-log
POST add
Content-Type: application/json

{"key":"value"}
###
// @no-log
GET first
###
GET second
// @no-log


###
""";
            var counter = 0;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("add", method.String);
                counter++;
                return Task.FromResult((401, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(1, counter);
            Assert.Equal("GET second" + Environment.NewLine, result);
        }

        [Fact]
        public async Task ParseMultiMethodWithJsonPayloadRunOnErrorTest()
        {
            const string req = """
# @no-log
error
Content-Type: application/json

{"key":"value"}
###
// @no-log
// @on-error
catch
###
second


###
""";
            var res = "second" +
                Environment.NewLine +
                "// @skipped reason = error" +
                Environment.NewLine +
                "###" +
                Environment.NewLine +
                Environment.NewLine;
            var counter = 0;
            var methods = new[] { "error", "catch" };
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal(methods[counter], method.String);
                counter++;
                return Task.FromResult((401, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(2, counter);
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParseMultiMethodWithJsonPayloadContinueOnErrorTest()
        {
            const string req = """
# @no-log
POST add
Content-Type: application/json

{"key":"value"}
// @continue-on-error
###
# @no-log
// @delay 0
GET first
// @continue-on-error
###
GET second
// @no-log
// @continue-on-error
###
""";
            var counter = 0;
            var methods = new[] { "add", "first", "second" };
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal(methods[counter], method.String);
                counter++;
                return Task.FromResult((401, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(3, counter);
            Assert.Equal("GET second" + Environment.NewLine, result);
        }

        [Fact]
        public async Task ParsePostMethodWithJsonPayloadAndHeadersTest()
        {
            const string req = """
POST method HTTP/1.1
Content-Type: application/json
Authorization: Bearer token
""";
            var res = "POST method HTTP/1.1" + Environment.NewLine + "200"
                    + Environment.NewLine + Environment.NewLine;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal("Bearer token", h["Authorization"]);
                return Task.FromResult((200, ReadOnlyMemory<byte>.Empty));
            });
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParsePostMethodWithJsonPayloadAndHeadersAndResponseTest()
        {
            const string req = """
POST method HTTP/1.1
Content-Type: application/json
Authorization: Bearer token

{"key":"value"}
""";
            var res = "POST method HTTP/1.1" + Environment.NewLine + "200"
                    + Environment.NewLine + Environment.NewLine
                    + "###"
                    + Environment.NewLine + Environment.NewLine;
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal("Bearer token", h["Authorization"]);
                Assert.Equal(15, r.Length);
                Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(r.Span));
                return Task.FromResult((200, (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("###")));
            });
            Assert.Equal(res, result);
        }

        [Fact]
        public async Task ParsePostWithBadHeaderThrowsTest()
        {
            const string req = """
# comment1
POST method
## comment ##
Authorization

###
""";
            var ex = await Assert.ThrowsAsync<FormatException>(
                () => DotHttpFileParser.ParseAsync(req, (method, r, h, ct)
                => Task.FromResult((200, ReadOnlyMemory<byte>.Empty))));

            Assert.Equal("Invalid header (line #4: 'Authorization')", ex.Message);
        }

        [Fact]
        public async Task ParseBadMethodThrowsTest()
        {
            const string req = """
// Bad
FOO method
Authorization

###
""";
            var ex = await Assert.ThrowsAsync<FormatException>(
                () => DotHttpFileParser.ParseAsync(req, (method, r, h, ct)
                => Task.FromResult((200, ReadOnlyMemory<byte>.Empty))));

            Assert.Equal("Invalid method format (line #2: 'FOO method')", ex.Message);
        }

        [Fact]
        public async Task ParsePostMethodWithJsonPayloadAndHeadersAndResponseFileTest()
        {
            const string req = """
method
Content-Type: application/json
Authorization:Bearer token

< ./input.json

>> ./output.json

###

""";
            var res = "method" + Environment.NewLine + "200"
                    + Environment.NewLine + Environment.NewLine
                    + "###"
                    + Environment.NewLine + Environment.NewLine;
            var provider = new Mock<IFileProvider>();
            var file = new Mock<IFileInfoEx>();
            provider.Setup(provider => provider.GetFileInfo(It.IsAny<string>()))
                .Returns(file.Object);
            file.SetupGet(file => file.Exists).Returns(true);
            file.Setup(file => file.CreateReadStream())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"key\":\"value\"}")));
            var output = new MemoryStream();
            file.SetupGet(file => file.IsWritable).Returns(true);
            file.Setup(file => file.CreateWriteStream())
                .Returns(output);

            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/json", h["Content-Type"]);
                Assert.Equal("Bearer token", h["Authorization"]);
                Assert.Equal(15, r.Length);
                Assert.Equal("{\"key\":\"value\"}", Encoding.UTF8.GetString(r.Span));
                return Task.FromResult((200, (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("{\"a\":\"b\"}")));
            }, null, provider.Object, default);

            Assert.Equal(res, result);
            Assert.Equal("{\"a\":\"b\"}", Encoding.UTF8.GetString(output.ToArray()));
        }

        [Fact]
        public async Task ParsePostMethodWithBinaryPayloadAndHeadersAndResponseFileTest()
        {
            const string req = """
method
Content-Type: application/binary
Authorization: Bearer token

// Override
>>! ./output.bin

###

""";
            var res = "method" + Environment.NewLine + "// Override"
                    + Environment.NewLine + "200"
                    + Environment.NewLine + Environment.NewLine
                    + "###"
                    + Environment.NewLine + Environment.NewLine;
            var provider = new Mock<IFileProvider>();
            var file = new Mock<IFileInfoEx>();
            provider.Setup(provider => provider.GetFileInfo(It.IsAny<string>()))
                .Returns(file.Object);
            file.SetupGet(file => file.Exists).Returns(true);
            file.Setup(file => file.CreateReadStream())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"key\":\"value\"}")));
            var output = new MemoryStream();
            file.SetupGet(file => file.IsWritable).Returns(true);
            file.Setup(file => file.CreateWriteStream())
                .Returns(output);

            var guid = Guid.NewGuid();
            var result = await DotHttpFileParser.ParseAsync(req, (method, r, h, ct) =>
            {
                Assert.Equal("method", method.String);
                Assert.Equal("application/binary", h["Content-Type"]);
                Assert.Equal("Bearer token", h["Authorization"]);
                Assert.Equal(0, r.Length);
                return Task.FromResult((200, (ReadOnlyMemory<byte>)guid.ToByteArray()));
            }, null, provider.Object, default);

            Assert.Equal(res, result);
            Assert.Equal(guid, new Guid(output.ToArray()));
        }

        [Fact]
        public async Task ParseBadContentTypeTest()
        {
            const string req = """
PUT method
Content-Type: application/binary

{ "key": "value" }

###
""";
            var ex = await Assert.ThrowsAsync<FormatException>(
                () => DotHttpFileParser.ParseAsync(req, (method, r, h, ct)
                => Task.FromResult((200, ReadOnlyMemory<byte>.Empty))));

            Assert.Equal("Only json content type supported inline", ex.Message);
        }
    }
}
