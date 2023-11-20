// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Serializers
{
    using Furly.Extensions.Serializers;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.Net.Http.Headers;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Output formatter
    /// </summary>
    public class SerializerOutputFormatter : OutputFormatter
    {
        /// <summary>
        /// Create formatter
        /// </summary>
        /// <param name="serializer"></param>
        public SerializerOutputFormatter(ISerializer serializer)
        {
            _serializer = serializer;
            SupportedMediaTypes.Add(new MediaTypeHeaderValue(serializer.MimeType));
        }

        /// <inheritdoc/>
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            if (context.Object is null)
            {
                return;
            }
            try
            {
                var responseStream = context.HttpContext.Response.Body;
                var objectType = context.Object?.GetType() ?? context.ObjectType;
                var ct = context.HttpContext.RequestAborted;

                await _serializer.SerializeObjectAsync(responseStream, context.Object,
                    objectType, ct: ct).ConfigureAwait(false);

                await responseStream.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        private readonly ISerializer _serializer;
    }
}
