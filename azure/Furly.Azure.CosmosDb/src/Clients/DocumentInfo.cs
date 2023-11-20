// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Storage;
    using System;

    /// <summary>
    /// Document wrapper
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class DocumentInfo<T> : IDocumentInfo<T>
    {
        /// <inheritdoc/>
        public string Id => (string)Document["id"]!;

        /// <inheritdoc/>
        public T Value => Document.ConvertTo<T>()!;

        /// <inheritdoc/>
        public string Etag => (string)Document["_etag"]!;

        /// <summary>
        /// Document
        /// </summary>
        internal VariantValue Document { get; }

        /// <summary>
        /// Create document
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="id"></param>
        internal DocumentInfo(VariantValue doc, string? id = null)
        {
            Document = doc ?? throw new ArgumentNullException(nameof(doc));
            if (!string.IsNullOrEmpty(id))
            {
                Document["id"].AssignValue(id);
            }
        }
    }
}
