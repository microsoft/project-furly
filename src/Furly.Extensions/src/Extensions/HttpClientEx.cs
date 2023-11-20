// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Net.Http
{
    using System.Net.Http.Headers;
    using System.Net;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Furly;
    using Furly.Exceptions;
    using Furly.Extensions.Serializers;

    /// <summary>
    /// Http client factory extensions
    /// </summary>
    public static class HttpClientEx
    {
        /// <inheritdoc/>
        public static HttpRequestMessage SetContent(this HttpRequestMessage request,
            byte[] content, string? mediaType, Encoding? encoding)
        {
            var headerValue = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(mediaType) ? ContentMimeType.Binary : mediaType);
            if (encoding != null)
            {
                headerValue.CharSet = encoding.WebName;
            }
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = headerValue;
            return request;
        }

#if UNUSED

        /// <inheritdoc/>
        public static HttpRequestMessage SetContent(this HttpRequestMessage request,
            Stream content, int bufferSize,
            string? mediaType = null, Encoding? encoding = null) {
            var headerValue = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(mediaType) ? ContentMimeType.Binary : mediaType);
            if (encoding != null) {
                headerValue.CharSet = encoding.WebName;
            }
            request.Content = new StreamContent(content, bufferSize);
            request.Content.Headers.ContentType = headerValue;
            return request;
        }
#endif

        /// <summary>
        /// Set accept headers
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="request"></param>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
        public static void SetAcceptHeaders(this ISerializer serializer,
            HttpRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.AddHeader("Accept", serializer.MimeType);
            if (serializer.ContentEncoding != null)
            {
                request.AddHeader("Accept-Charset", serializer.ContentEncoding.WebName);
            }
        }

        /// <summary>
        /// Serialize to request
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="request"></param>
        /// <param name="content"></param>
        public static void SerializeToRequest(this ISerializer serializer,
            HttpRequestMessage request, object content)
        {
            serializer.SetAcceptHeaders(request);
            request.SetContent(serializer.SerializeObjectToMemory(
                content, content.GetType()).ToArray(),
                serializer.MimeType, serializer.ContentEncoding);
        }

        /// <summary>
        /// Wrap a client into a factory that only ever returns this client
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static IHttpClientFactory ToHttpClientFactory(this HttpClient client)
        {
            return new HttpClientFactoryWrapper(client);
        }

        /// <summary>
        /// Set request timeout
        /// </summary>
        /// <param name="request"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static HttpRequestMessage SetTimeout(this HttpRequestMessage request,
            TimeSpan? timeout)
        {
            request.Options.Set(kTimeoutKey, timeout);
            return request;
        }

        /// <summary>
        /// Get request timeout
        /// </summary>
        /// <param name="request"></param>
        public static TimeSpan? GetTimeout(this HttpRequestMessage request)
        {
            if (!request.Options.TryGetValue(kTimeoutKey, out var timeout))
            {
                return null;
            }
            return timeout;
        }

        private static readonly HttpRequestOptionsKey<TimeSpan?> kTimeoutKey = new("Timeout");

        /// <summary>
        /// Add header value
        /// </summary>
        /// <param name="request"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns>this</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static HttpRequestMessage AddHeader(this HttpRequestMessage request,
            string name, string? value)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value) &&
                !name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(name, "Invalid header name");
            }
            return request;
        }

        /// <inheritdoc/>
        public static Task<HttpResponseMessage> GetAsync(this IHttpClientFactory factory,
            HttpRequestMessage request, Func<Task<string?>>? authorization = null,
            CancellationToken ct = default)
        {
            request.Method = HttpMethod.Get;
            return factory.SendAsync(request, authorization: authorization, ct: ct);
        }

        /// <inheritdoc/>
        public static async Task<T> GetAsync<T>(this IHttpClientFactory factory,
            Uri uri, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null,
            CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(request);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request, authorization: authorization,
                ct: ct).ConfigureAwait(false);
            return await serializer.DeserializeResponseAsync<T>(response, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public static Task<HttpResponseMessage> PostAsync(this IHttpClientFactory factory,
            HttpRequestMessage request, Func<Task<string?>>? authorization = null,
            CancellationToken ct = default)
        {
            request.Method = HttpMethod.Post;
            return factory.SendAsync(request, authorization: authorization, ct: ct);
        }

        /// <inheritdoc/>
        public static async IAsyncEnumerable<T> GetStreamAsync<T>(
            this IHttpClientFactory factory, Uri uri, ISerializer serializer,
            Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            serializer.SetAcceptHeaders(request);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, authorization: authorization,
                ct: ct).ConfigureAwait(false);
            var results = serializer.DeserializeResponseAsStreamAsync<T>(response, ct);
            await foreach (var item in results.ConfigureAwait(false))
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc/>
        public static async Task PostAsync(this IHttpClientFactory factory,
            Uri uri, object content, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null,
            CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            response.ValidateResponse();
        }

        /// <inheritdoc/>
        public static async Task<T> PostAsync<T>(this IHttpClientFactory factory,
            Uri uri, object content, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            return await serializer.DeserializeResponseAsync<T>(response, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public static async IAsyncEnumerable<T> PostStreamAsync<T>(
            this IHttpClientFactory factory, Uri uri, object content,
            ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, authorization: authorization,
                ct: ct).ConfigureAwait(false);
            var results = serializer.DeserializeResponseAsStreamAsync<T>(response, ct);
            await foreach (var item in results.ConfigureAwait(false))
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc/>
        public static async Task PutAsync(this IHttpClientFactory factory,
            Uri uri, object content, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            response.ValidateResponse();
        }

        /// <inheritdoc/>
        public static async Task<T> PutAsync<T>(this IHttpClientFactory factory,
            Uri uri, object content, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            return await serializer.DeserializeResponseAsync<T>(response, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public static async Task PatchAsync(this IHttpClientFactory factory,
            Uri uri, object content, ISerializer serializer, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri);
            serializer.SerializeToRequest(request, content);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            response.ValidateResponse();
        }

        /// <inheritdoc/>
        public static async Task DeleteAsync(this IHttpClientFactory factory,
            Uri uri, Action<HttpRequestMessage>? configure = null,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            configure?.Invoke(request);
            using var response = await factory.SendAsync(request,
                authorization: authorization, ct: ct).ConfigureAwait(false);
            response.ValidateResponse();
        }

        /// <summary>
        /// Deserialize from response
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="response"></param>
        /// <param name="ct"></param>
        /// <exception cref="SerializerException"></exception>
        public static async Task<T> DeserializeResponseAsync<T>(this ISerializer serializer,
            HttpResponseMessage response, CancellationToken ct = default)
        {
            response.ValidateResponse(true);
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            try
            {
                if (serializer.ContentEncoding != null)
                {
                    var desired = response.GetEncoding();
                    if (desired != serializer.ContentEncoding)
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        stream = Encoding.CreateTranscodingStream(stream, desired,
                            serializer.ContentEncoding);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }
                }
                var typed = await serializer.DeserializeAsync<T>(stream,
                    ct: ct).ConfigureAwait(false);
                if (typed is null)
                {
                    throw new SerializerException(
                        $"Failed to serialize type {typeof(T).Name} from response.");
                }
                return typed;
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deserialize from response
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="response"></param>
        /// <param name="ct"></param>
        public static async IAsyncEnumerable<T?> DeserializeResponseAsStreamAsync<T>(
            this ISerializer serializer, HttpResponseMessage response,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            response.ValidateResponse(true);
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            try
            {
                if (serializer.ContentEncoding != null)
                {
                    var desired = response.GetEncoding();
                    if (desired != serializer.ContentEncoding)
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        stream = Encoding.CreateTranscodingStream(stream, desired,
                            serializer.ContentEncoding);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }
                }
                await foreach (var result in serializer.DeserializeStreamAsync<T>(
                    stream, ct).ConfigureAwait(false))
                {
                    yield return result;
                }
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Helper method to validate status code
        /// </summary>
        /// <param name="response"></param>
        /// <param name="throwOnError"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="BadRequestException"></exception>
        /// <exception cref="ResourceInvalidStateException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="ResourceNotFoundException"></exception>
        /// <exception cref="ResourceConflictException"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ResourceOutOfDateException"></exception>
        /// <exception cref="HttpTransientException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        /// <exception cref="IOException"></exception>
        public static bool ValidateResponse(this HttpResponseMessage response,
            bool throwOnError = true)
        {
            if ((int)response.StatusCode < 400 && response.StatusCode != 0)
            {
                return true;
            }
            if (!throwOnError)
            {
                return false;
            }
            switch (response.StatusCode)
            {
                case HttpStatusCode.MethodNotAllowed:
                    throw new InvalidOperationException(Message(response));
                case HttpStatusCode.NotAcceptable:
                case HttpStatusCode.BadRequest:
                    throw new BadRequestException(Message(response));
                case HttpStatusCode.Forbidden:
                    throw new ResourceInvalidStateException(Message(response));
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException(Message(response));
                case HttpStatusCode.NotFound:
                    throw new ResourceNotFoundException(Message(response));
                case HttpStatusCode.Conflict:
                    throw new ResourceConflictException(Message(response));
                case HttpStatusCode.RequestTimeout:
                    throw new TimeoutException(Message(response));
                case HttpStatusCode.PreconditionFailed:
                    throw new ResourceOutOfDateException(Message(response));
                case HttpStatusCode.InternalServerError:
                    throw new ResourceInvalidStateException(Message(response));
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.TemporaryRedirect:
                    // Retried
                    throw new HttpTransientException(response.StatusCode, Message(response));
                case HttpStatusCode.TooManyRequests:
                    // Retried
                    throw new HttpTransientException(response.StatusCode, Message(response));
                default:
                    throw new HttpRequestException(Message(response), null, response.StatusCode);
            }

            static string Message(HttpResponseMessage response)
            {
                try
                {
                    var buffer = response.Content.ReadAsByteArray();
                    if (buffer.Array == null)
                    {
                        throw new IOException("Failed to read from stream");
                    }
                    return Encoding.UTF8.GetString(buffer.Array, 0, buffer.Count);
                }
                catch
                {
                    return response.StatusCode.ToString();
                }
            }
        }

        /// <summary>
        /// Retriable exception
        /// </summary>
        internal class HttpTransientException : HttpRequestException, ITransientException
        {
            /// <inheritdoc />
            public HttpTransientException(HttpStatusCode statusCode, string message) :
                base(message, null, statusCode)
            {
            }

            /// <inheritdoc />
            public HttpTransientException()
            {
            }

            /// <inheritdoc />
            public HttpTransientException(string? message)
                : base(message)
            {
            }

            /// <inheritdoc />
            public HttpTransientException(string? message, Exception? inner)
                : base(message, inner)
            {
            }

            /// <inheritdoc />
            public HttpTransientException(string? message, Exception? inner,
                HttpStatusCode? statusCode) : base(message, inner, statusCode)
            {
            }
        }

        /// <summary>
        /// Send request
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="httpRequest"></param>
        /// <param name="options"></param>
        /// <param name="authorization"></param>
        /// <param name="ct"></param>
        internal static async Task<HttpResponseMessage> SendAsync(this IHttpClientFactory factory,
            HttpRequestMessage httpRequest,
            HttpCompletionOption options = HttpCompletionOption.ResponseContentRead,
            Func<Task<string?>>? authorization = null, CancellationToken ct = default)
        {
            using (var client = factory.CreateClient())
            {
                var timeout = httpRequest.GetTimeout();
                if (timeout != null)
                {
                    client.Timeout = timeout.Value;
                }
                if (authorization != null)
                {
                    var token = await authorization().ConfigureAwait(false);
                    if (token != null)
                    {
                        httpRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
                    }
                }
                return await client.SendAsync(httpRequest, options, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Get encoding from response
        /// </summary>
        /// <param name="response"></param>
        internal static Encoding GetEncoding(this HttpResponseMessage response)
        {
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (charset != null)
            {
                try
                {
                    // Remove at most a single set of quotes.
                    if (charset.Length > 2 && charset[0] == '\"' && charset[^1] == '\"')
                    {
                        charset = charset[1..^1];
                    }
                    return Encoding.GetEncoding(charset);
                }
                catch
                {
                    return Encoding.UTF8;
                }
            }
            return Encoding.UTF8;
        }

        /// <summary>
        /// Helper extension to convert an entire stream into a buffer...
        /// </summary>
        /// <param name="content"></param>
        private static ArraySegment<byte> ReadAsByteArray(this HttpContent content)
        {
            var stream = content.ReadAsStream();
            // Try to read as much as possible
            var body = new byte[1024];
            var offset = 0;
            try
            {
                while (true)
                {
                    var read = stream.Read(body, offset, body.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                    if (offset == body.Length)
                    {
                        // Grow
                        var newbuf = new byte[body.Length * 2];
                        Buffer.BlockCopy(body, 0, newbuf, 0, body.Length);
                        body = newbuf;
                    }
                }
            }
            catch (IOException) { }
            return new ArraySegment<byte>(body, 0, offset);
        }

        /// <summary>
        /// Try and wrap a http client into a factory. The factory returns
        /// a wrapper of the client to override IDisposable, but that also
        /// means that some settings of the original client are potentially
        /// lost especially default headers.
        /// </summary>
        private sealed class HttpClientFactoryWrapper : IHttpClientFactory
        {
            public HttpClientFactoryWrapper(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClientWrapper(_client)
                {
                    BaseAddress =
                        _client.BaseAddress,
                    DefaultRequestVersion =
                        _client.DefaultRequestVersion,
                    DefaultVersionPolicy =
                        _client.DefaultVersionPolicy,
                    MaxResponseContentBufferSize =
                        _client.MaxResponseContentBufferSize,
                    Timeout =
                        _client.Timeout
                };
            }

            private sealed class HttpClientWrapper : HttpClient
            {
                private readonly HttpClient _client;

                public HttpClientWrapper(HttpClient client)
                {
                    _client = client;

                    // TODO: Update default headers from client
                }

                public override bool Equals(object? obj)
                {
                    return _client.Equals(obj);
                }

                public override int GetHashCode()
                {
                    return _client.GetHashCode();
                }

                public override HttpResponseMessage Send(
                    HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    return _client.Send(request, cancellationToken);
                }

                public override Task<HttpResponseMessage> SendAsync(
                    HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    return _client.SendAsync(request, cancellationToken);
                }

                public override string? ToString()
                {
                    return _client.ToString();
                }
            }

            private readonly HttpClient _client;
        }
    }
}
