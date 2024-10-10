// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Rpc.Servers
{
    using Furly.Extensions.Storage;
    using Microsoft.Extensions.FileProviders;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Callback
    /// </summary>
    /// <param name="method"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    internal delegate Task<(int status, ReadOnlyMemory<byte> response)> Execute(
        Method method, ReadOnlyMemory<byte> request, Dictionary<string, string> headers,
        CancellationToken ct);

    /// <summary>
    /// Method
    /// </summary>
    /// <param name="String"></param>
    /// <param name="Uri"></param>
    /// <param name="ProtocolVersion"></param>
    internal sealed record class Method(string String, Uri? Uri = null,
        string? ProtocolVersion = null);

    /// <summary>
    /// Simple .http parser. Does not support multiline, scripting or environment
    /// variables
    /// </summary>
    internal sealed class DotHttpFileParser : IAsyncDisposable
    {
        /// <summary>
        /// Create parser
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="execute"></param>
        /// <param name="root"></param>
        /// <param name="provider"></param>
        /// <param name="ct"></param>
        public DotHttpFileParser(Stream request, Stream response, Execute execute,
            string? root = null, IFileProvider? provider = null, CancellationToken ct = default)
        {
            _root = root ?? Directory.GetCurrentDirectory();
            _request = new StreamReader(request, leaveOpen: true);
            _response = new StreamWriter(response, leaveOpen: true);
            _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _provider = provider;
            _execute = execute;
            _parser = ParseAsync(ct);
        }

        /// <summary>
        /// Parse
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="execute"></param>
        /// <param name="root"></param>
        /// <param name="provider"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task ParseAsync(Stream request, Stream response,
            Execute execute, string? root = null, IFileProvider? provider = null,
            CancellationToken ct = default)
        {
            await using var parser = new DotHttpFileParser(request,
                response, execute, root, provider, ct: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Parse
        /// </summary>
        /// <param name="request"></param>
        /// <param name="execute"></param>
        /// <param name="root"></param>
        /// <param name="provider"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task<string> ParseAsync(string request,
            Execute execute, string? root = null, IFileProvider? provider = null,
            CancellationToken ct = default)
        {
            var req = new MemoryStream(Encoding.UTF8.GetBytes(request));
            await using (req.ConfigureAwait(false))
            {
                var res = new MemoryStream();
                await using (res.ConfigureAwait(false))
                {
                    await DotHttpFileParser.ParseAsync(req, res, execute,
                        root, provider, ct).ConfigureAwait(false);
                    return Encoding.UTF8.GetString(res.ToArray());
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _parser.ConfigureAwait(false);
            }
            finally
            {
                _response.Dispose();
                _request.Dispose();
            }
        }

        /// <summary>
        /// Parse request and response files and execute
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ParseAsync(CancellationToken ct)
        {
            while (!_executionFailure && !_directives.HasFlag(Directive.NoContinue))
            {
                var line = ReadLine();

                // End of file or seperator reached?
                if (line?.StartsWith("###", StringComparison.Ordinal) != false)
                {
                    await ExecuteAsync(ct).ConfigureAwait(false);

                    // EOF?
                    if (line == null)
                    {
                        break;
                    }

                    // Write the ### seperator and possible comments and reset state
                    WriteLine(line);
                    WriteLine();
                    Reset();
                    continue;
                }

                if (ParseComment(line))
                {
                    continue;
                }

                if (line.Length == 0)
                {
                    // Empty line is a seperator between headers and body
                    if (_state == State.Headers)
                    {
                        _state = State.Body;
                    }

                    if (_state != State.Body)
                    {
                        // We skip all empty lines unless we are in
                        // inline body
                        continue;
                    }
                }

                switch (_state)
                {
                    case State.Method:
                        ParseMethod(line);
                        break;
                    case State.Headers:
                        ParseHeaders(line);
                        break;
                    case State.Body:
                        ParseBody(line);
                        break;
                }
            }
        }

        private bool ParseComment(string line)
        {
            var comment = line;
            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                comment = comment.TrimStart('/').Trim();
            }
            else if (line.StartsWith('#'))
            {
                comment = comment.TrimStart('#').Trim();
            }
            else
            {
                return false;
            }

            if (comment.StartsWith('@'))
            {
                switch (comment)
                {
                    case "@no-log":
                        _directives |= Directive.NoLog;
                        break;
                    case "@stop-on-error":
                        _directives |= Directive.NoContinue;
                        break;
                    default:
                        ThrowFormatException("Unsupported directive", line);
                        break;
                }
                return true;
            }

            // Parse through comments but skip
            WriteLine(line);
            return true;
        }

        /// <summary>
        /// Parse method line
        /// </summary>
        /// <param name="line"></param>
        private void ParseMethod(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var method = parts[0].ToUpperInvariant();
                // Check if this is a http method
                switch (method)
                {
                    case "POST":
                    case "GET":
                    case "PUT":
                    case "DELETE":
                    case "PATCH":
                    case "OPTIONS":
                        try
                        {
                            var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
                            if (!string.IsNullOrEmpty(uri.PathAndQuery))
                            {
                                _method = new Method(method, uri,
                                    parts.Length >= 3 ? parts[2] : null);
                                break;
                            }
                        }
                        catch { }
                        _method = new Method(parts[1]);
                        break;
                    default:
                        ThrowFormatException("Invalid method format", line);
                        return;
                }
            }
            else
            {
                _method = new Method(line);
            }

            // Capture and write method to output
            WriteLine(line);
            _state = State.Headers;
        }

        /// <summary>
        /// Parse headers
        /// </summary>
        /// <param name="line"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void ParseHeaders(string line)
        {
            var idx = line.IndexOf(':', StringComparison.Ordinal);
            if (idx < 0)
            {
                ThrowFormatException("Invalid header", line);
            }

            var k = line.Substring(0, idx).Trim();
            var v = line.Substring(idx + 1).Trim();
            _headers.Add(k, v);
        }

        /// <summary>
        /// Parse body
        /// </summary>
        /// <param name="line"></param>
        private void ParseBody(string line)
        {
            if (line.StartsWith('<'))
            {
                _input = line.Substring(1, line.Length - 1).Trim();
            }
            else if (line.StartsWith(">>!", StringComparison.Ordinal))
            {
                _append = true;
                _output = line.Substring(3, line.Length - 3).Trim();
            }
            else if (line.StartsWith(">>", StringComparison.Ordinal))
            {
                _append = false;
                _output = line.Substring(2, line.Length - 2).Trim();
            }
            else
            {
                _body += line;
            }
        }

        /// <summary>
        /// Execute the request
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        private async Task ExecuteAsync(CancellationToken ct)
        {
            if (_method == null)
            {
                // Nothing to do
                return;
            }

            // Get content type
            if (!_headers.TryGetValue("Content-Type", out var contentType))
            {
                contentType = "application/json";
            }

            var jsonPayload = contentType == "application/json";
            var payload = Encoding.UTF8.GetBytes(_body.Trim() ?? string.Empty);
            if (!string.IsNullOrEmpty(_input))
            {
                payload = await ReadFileAsync(_input, ct).ConfigureAwait(false);
            }
            else if (!jsonPayload && payload.Length > 0)
            {
                throw new FormatException("Only json content type supported inline");
            }

            var (status, result) = await _execute(_method, payload, _headers,
                ct).ConfigureAwait(false);

            // Write status and result
            WriteLine(status.ToString(CultureInfo.InvariantCulture));
            WriteLine();

            if (!string.IsNullOrEmpty(_output))
            {
                await WriteFileAsync(_output, _append, result, ct).ConfigureAwait(false);
            }
            else if (jsonPayload && result.Length > 0)
            {
                WriteLine(Encoding.UTF8.GetString(result.Span));
                WriteLine();
            }

            _executionFailure = status >= 400;
        }

        /// <summary>
        /// Write to file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="append"></param>
        /// <param name="result"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task WriteFileAsync(string file, bool append,
            ReadOnlyMemory<byte> result, CancellationToken ct)
        {
            file = Path.GetFileName(file);
            var stream = _provider?.GetFileInfo(file) is IFileInfoEx fi
                && fi.IsWritable
                ? fi.CreateWriteStream()
                : File.Open(Path.Combine(_root, file), append
                    ? FileMode.Append : FileMode.Create);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(result, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Read from file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<byte[]> ReadFileAsync(string file, CancellationToken ct)
        {
            file = Path.GetFileName(file);
            if (_provider != null)
            {
                var stream = _provider.GetFileInfo(file).CreateReadStream();
                await using (stream.ConfigureAwait(false))
                {
                    var payload = new MemoryStream();
                    await using (payload.ConfigureAwait(false))
                    {
                        await stream.CopyToAsync(payload, ct).ConfigureAwait(false);
                        return payload.ToArray();
                    }
                }
            }
            return await File.ReadAllBytesAsync(Path.Combine(_root, file),
                ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Write line
        /// </summary>
        /// <param name="line"></param>
        private void WriteLine(string? line = null)
        {
            if (_directives.HasFlag(Directive.NoLog))
            {
                return;
            }
            _response.WriteLine(line);
        }

        /// <summary>
        /// Read line
        /// </summary>
        /// <returns></returns>
        private string? ReadLine()
        {
            // TODO: Next line with indent is still same line as per spec.

            var line = _request.ReadLine()?.Trim();
            if (line != null)
            {
                _lineNumber++;
            }
            return line;
        }

        /// <summary>
        /// Throw exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="line"></param>
        /// <exception cref="FormatException"></exception>
        private void ThrowFormatException(string message, string line)
        {
            throw new FormatException($"{message} (line #{_lineNumber}: '{line}')");
        }

        /// <summary>
        /// Reset
        /// </summary>
        private void Reset()
        {
            _body = string.Empty;
            _method = null;
            _input = string.Empty;
            _output = string.Empty;
            _append = false;
            _headers.Clear();
            _state = State.Method;
            _directives = 0;
        }

        /// <summary>
        /// Parser state
        /// </summary>
        private enum State
        {
            Method,
            Headers,
            Body
        }

        [Flags]
        private enum Directive
        {
            NoLog = 1,
            NoContinue = 2,
        }

        private readonly Dictionary<string, string> _headers;
        private readonly StreamReader _request;
        private readonly StreamWriter _response;
        private readonly IFileProvider? _provider;
        private readonly Execute _execute;
        private readonly Task _parser;
        private readonly string _root;
        private int _lineNumber;
        private Method? _method;
        private bool _executionFailure;
        private string _body = string.Empty;
        private string _input = string.Empty;
        private bool _append;
        private Directive _directives;
        private string _output = string.Empty;
        private State _state = State.Method;
    }
}
