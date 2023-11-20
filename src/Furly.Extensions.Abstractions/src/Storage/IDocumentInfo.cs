// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    /// <summary>
    /// Document in the document database
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDocumentInfo<T>
    {
        /// <summary>
        /// Id of the resource
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Value
        /// </summary>
        T Value { get; }

        /// <summary>
        /// Etag of the document
        /// </summary>
        string Etag { get; }
    }
}
