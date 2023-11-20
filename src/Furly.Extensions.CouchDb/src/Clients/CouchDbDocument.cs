// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.Storage;
    using CouchDB.Driver.Types;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Document wrapper
    /// </summary>
    internal sealed class CouchDbDocument : CouchDocument, IDocumentInfo<JToken>
    {
        /// <summary>
        /// The actual document values to serialize
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> Document { get; }

        /// <summary>
        /// Create document
        /// </summary>
        public CouchDbDocument()
        {
            Document = new Dictionary<string, JToken>();
        }

        /// <summary>
        /// Create document
        /// </summary>
        /// <param name="document"></param>
        public CouchDbDocument(IDictionary<string, JToken> document)
        {
            Document = document;
        }

        /// <inheritdoc/>
        [JsonIgnore]
        public JToken Value
        {
            get
            {
                if (_value is null)
                {
                    var o = JObject.FromObject(Document);

                    // Add etag and id as per convention
                    o.AddOrUpdate(kIdProperty, Id);
                    o.AddOrUpdate(kEtagProperty, Etag);

                    _value = o;
                }
                return _value;
            }
        }

        /// <inheritdoc/>
        [JsonIgnore]
        public string Etag
        {
            get => Rev ?? string.Empty;
            set => Rev = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Convert to typed document
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal IDocumentInfo<T> ToDocumentInfo<T>()
        {
            return new TypedDocument<T>(this);
        }

        /// <summary>
        /// Create document
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="id"></param>
        /// <param name="etag"></param>
        internal static CouchDbDocument Wrap<T>(T value, string? id, string? etag)
        {
            var token = value is null ? JValue.CreateNull() : JToken.FromObject(value);
            if (token is JObject o)
            {
                return WrapJson(o, id, etag);
            }
            return new CouchDbDocument { _value = token };
        }

        /// <summary>
        /// Create document
        /// </summary>
        /// <param name="o"></param>
        /// <param name="id"></param>
        /// <param name="etag"></param>
        internal static CouchDbDocument WrapJson(JObject o, string? id, string? etag)
        {
            var doc = new CouchDbDocument(o.Properties().ToDictionary(p => p.Name, p => p.Value));
            if (!string.IsNullOrWhiteSpace(id))
            {
                doc.Id = id;
            }
            else if (doc.Document.TryGetValue(kIdProperty, out var jid))
            {
                doc.Id = (string?)jid;
            }
            else
            {
                doc.Id = Guid.NewGuid().ToString();
            }
            if (!string.IsNullOrWhiteSpace(etag))
            {
                doc.Rev = etag;
            }
            else if (doc.Document.TryGetValue(kEtagProperty, out var jetag))
            {
                doc.Rev = (string?)jetag;
            }
            else
            {
                // new document - let database assign.
                doc.Rev = null;
            }
            // Remove any occurrence of id to avoid duplication
            doc.Document.Remove(kIdProperty);
            doc.Document.Remove(kEtagProperty);
            return doc;
        }

        /// <summary>
        /// Typed version of document
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private sealed class TypedDocument<T> : IDocumentInfo<T>
        {
            /// <summary>
            /// Typed doc
            /// </summary>
            /// <param name="doc"></param>
            public TypedDocument(CouchDbDocument doc)
            {
                _doc = doc;
            }

            /// <inheritdoc/>
            public string Id => _doc.Id;
            /// <inheritdoc/>
            public T Value => _doc.Value.ToObject<T>()!;
            /// <inheritdoc/>
            public string Etag => _doc.Etag;

            private readonly CouchDbDocument _doc;
        }

        private const string kIdProperty = "id";
        private const string kEtagProperty = "_etag";
        private JToken? _value;
    }
}
