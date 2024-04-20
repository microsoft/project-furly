// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Storage
{
    using Furly.Extensions.Messaging;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// File writer
    /// </summary>
    public interface IFileWriter
    {
        /// <summary>
        /// Content type
        /// </summary>
        bool SupportsContentType(string contentType);

        /// <summary>
        /// Write to file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="timestamp"></param>
        /// <param name="buffers"></param>
        /// <param name="metadata"></param>
        /// <param name="schema"></param>
        /// <param name="contentType"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask WriteAsync(string fileName, DateTime timestamp,
            IEnumerable<ReadOnlySequence<byte>> buffers,
            IReadOnlyDictionary<string, string?> metadata,
            IEventSchema? schema, string contentType,
            CancellationToken ct = default);
    }
}
