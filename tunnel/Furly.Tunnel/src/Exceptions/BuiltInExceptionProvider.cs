// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions
{
    using Furly.Exceptions;
    using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Built in exceptions
    /// </summary>
    public class BuiltInExceptionProvider : IExceptionSummaryProvider
    {
        /// <inheritdoc/>
        public IEnumerable<Type> SupportedExceptionTypes => _supported.Keys;

        /// <inheritdoc/>
        public IReadOnlyList<string> Descriptions => _descriptions;

        /// <inheritdoc/>
        public int Describe(Exception exception, out string? additionalDetails)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (exception is OperationCanceledException)
            {
                additionalDetails = "Reason unknown";
            }
            else
            {
                additionalDetails = exception.Message;
            }
            if (_supported.TryGetValue(exception.GetType(), out var index))
            {
                return index;
            }
            foreach (var supportedType in _supported)
            {
                if (exception.GetType().IsAssignableFrom(supportedType.Key))
                {
                    return supportedType.Value;
                }
            }
            return 0;
        }

        /// <summary>
        /// Register types
        /// </summary>
        static BuiltInExceptionProvider()
        {
            var descriptions = new Dictionary<Type, string>
            {
                [typeof(Exception)] =
                    "Unknown exception",
                [typeof(ResourceExhaustionException)] =
                    "Thrown when a resource is exhausted and the system cannot " +
                    "handle the operation",
                [typeof(ResourceInvalidStateException)] =
                    "A resource is in a state that does not allow the operation to continue.",
                [typeof(ResourceOutOfDateException)] =
                    "A resource cannot be updated because it is not in the expected state. " +
                    "This can happen when another operation has modified the resource.",
                [typeof(ResourceNotFoundException)] =
                    "The requested resource could not be found.",
                [typeof(ExternalDependencyException)] =
                    "An external system is not available or returned an error.",
                [typeof(BadRequestException)] =
                    "The request contains invalid information " +
                    "or the parameters of the operation are invalid.",
                [typeof(InvalidConfigurationException)] =
                    "The configuration provided to the system is invalid.",
                [typeof(MethodCallStatusException)] =
                    "A method call resulted in an error with an explicit error detail provided.",
                [typeof(MethodCallException)] =
                    "A method call resulted in an error.",
                [typeof(MessageSizeLimitException)] =
                    "The message is too large for the system to handle.",
                [typeof(TemporarilyBusyException)] =
                    "The system is termporarily busy, please try again later.",
                [typeof(SerializerException)] =
                    "Serialization or deserialization of data failed. " +
                    "This can occur if the input is malformed.",
                [typeof(StorageException)] =
                    "Accessing persistent storage failed.",
                [typeof(ResourceConflictException)] =
                    "The operation failed because the specified resource or entity already " +
                    "exist and cannot be added again.",
                [typeof(NotSupportedException)] =
                    "The operation is not supported. This could be due to configuration " +
                    "expclitly disabling the capability.",
                [typeof(NotImplementedException)] =
                    "The operation has not yet been implemented.",
                [typeof(TimeoutException)] =
                    "The operation timed out after the configured or specified timeout duration.",
                [typeof(OperationCanceledException)] =
                    "The operation was cancelled by the system or due to user action.",
                [typeof(TaskCanceledException)] =
                    "The operation was cancelled by the system or due to user action.",
                [typeof(NotInitializedException)] =
                    "The resource is not initialized correctly.",
                [typeof(CommunicationException)] =
                    "An error occurred communicating with another entity.",
                [typeof(ResourceTooLargeException)] =
                    "The resource is too large for the operation to complete.",
                [typeof(ResourceUnauthorizedException)] =
                    "The operation failed because the user does not have permissions.",
                [typeof(ArgumentNullException)] =
                    "A parameter of an operation was unexpectedly null.",
                [typeof(ArgumentException)] =
                    "A parameter of an operation was invalid.",
                [typeof(ArgumentOutOfRangeException)] =
                    "A parameter of an operation was outside of the allowed range."
            };

            _descriptions = descriptions.Values.ToImmutableArray();
            _supported = descriptions.Keys
                .Select((v, i) => KeyValuePair.Create(v, i))
                .Skip(1)
                .ToImmutableDictionary();
        }
        private static readonly ImmutableArray<string> _descriptions;
        private static readonly ImmutableDictionary<Type, int> _supported;
    }
}
