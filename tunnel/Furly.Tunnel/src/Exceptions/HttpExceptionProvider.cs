﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
    using System;
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Http exception diagnosis for telemetry.
    /// </summary>
    internal sealed class HttpExceptionProvider : IExceptionSummaryProvider
    {
        /// <inheritdoc/>
        public IEnumerable<Type> SupportedExceptionTypes { get; } =
        [
            typeof(WebException),
            typeof(SocketException),
        ];

        /// <inheritdoc/>
        public IReadOnlyList<string> Descriptions => kDescriptions;

        /// <inheritdoc/>
        public int Describe(Exception exception, out string? additionalDetails)
        {
            ArgumentNullException.ThrowIfNull(exception);
            additionalDetails = null;
            switch (exception)
            {
                case OperationCanceledException ex:
                    return ex.CancellationToken.IsCancellationRequested ? 0 : 1;

                case WebException ex:
                    if (kWebExceptionStatusMap.TryGetValue(ex.Status, out var webidx))
                    {
                        return webidx;
                    }
                    break;
                case SocketException ex:
                    if (kSocketErrorMap.TryGetValue(ex.SocketErrorCode, out var sidx))
                    {
                        return sidx;
                    }
                    break;
            }
            return -1;
        }

        static HttpExceptionProvider()
        {
            var descriptions = new List<string>();

            var socketErrors = new Dictionary<SocketError, int>();
            foreach (var socketError in Enum.GetValues<SocketError>())
            {
                var name = socketError.ToString();

                socketErrors[socketError] = descriptions.Count;
                descriptions.Add(name);
            }

            var webStatuses = new Dictionary<WebExceptionStatus, int>();
            foreach (var status in Enum.GetValues<WebExceptionStatus>())
            {
                var name = status.ToString();

                webStatuses[status] = descriptions.Count;
                descriptions.Add(name);
            }

            kDescriptions = [.. descriptions];
            kSocketErrorMap = socketErrors.ToFrozenDictionary();
            kWebExceptionStatusMap = webStatuses.ToFrozenDictionary();
        }

        private static readonly FrozenDictionary<WebExceptionStatus, int> kWebExceptionStatusMap;
        private static readonly FrozenDictionary<SocketError, int> kSocketErrorMap;
        private static readonly ImmutableArray<string> kDescriptions;
    }
}
