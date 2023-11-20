// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System
{
    using System.IO;
    using System.Linq.Expressions;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Flurl.Http;
    using Furly.Exceptions;
    using Newtonsoft.Json;

    internal static class CouchDbExtensions
    {
        /// <summary>
        /// Get property name
        /// </summary>
        /// <param name="memberInfo"></param>
        internal static string GetPropertyName(this MemberInfo memberInfo)
        {
            var datamember = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
            var name = datamember?.Name ?? memberInfo.Name;
            if (name == "id")
            {
                name = "_id"; // Translate from convention to couchdb id property
            }
            return name;
        }

        internal static bool IsExpressionOfFunc(this Type type, int funcGenericArgs = 2)
        {
            return type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Expression<>) &&
                type.GetGenericArguments()[0].IsGenericType &&
                type.GetGenericArguments()[0].GetGenericArguments().Length == funcGenericArgs;
        }

        /// <summary>
        /// Get lambda expression from method call
        /// </summary>
        /// <param name="node"></param>
        internal static LambdaExpression GetLambda(this MethodCallExpression node)
        {
            var e = node.Arguments[1];
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            return (LambdaExpression)e;
        }

        /// <summary>
        /// Check success
        /// </summary>
        /// <param name="response"></param>
        public static bool IsSuccessful(this IFlurlResponse response)
        {
            return
                response.StatusCode is ((int)HttpStatusCode.OK) or
                ((int)HttpStatusCode.Created) or
                ((int)HttpStatusCode.Accepted) or
                ((int)HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Send request
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="asyncRequest"></param>
        /// <param name="message"></param>
        internal static async Task<TResult> SendRequestAsync<TResult>(
            this Task<TResult> asyncRequest, string? message = null)
        {
            var attempt = 1;
            while (true)
            {
                try
                {
                    return await asyncRequest.ConfigureAwait(false);
                }
                catch (FlurlHttpException ex)
                {
                    if (ex.InnerException is HttpRequestException hr &&
                        hr.InnerException is IOException io &&
                        attempt < 5)
                    {
                        // Try again
                        attempt++;
                        continue;
                    }
                    throw await ex.TranslateExceptionAsync(message).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Rethrow
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        internal static async Task<Exception> TranslateExceptionAsync(
            this FlurlHttpException ex, string? message = null)
        {
            var couchError = await ex.GetResponseJsonAsync<OperationError>()
                .ConfigureAwait(false) ?? new OperationError();
            return ex.Call?.HttpResponseMessage?.StatusCode switch
            {
                HttpStatusCode.Conflict => new ResourceConflictException(
                    couchError.ToString(message), ex),
                HttpStatusCode.NotFound => new ResourceNotFoundException(
                    couchError.ToString(message), ex),
                HttpStatusCode.BadRequest when couchError.Error == "no_usable_index" =>
                    new ResourceNotFoundException(couchError.ToString(message), ex),
                HttpStatusCode.BadRequest when couchError.Reason == "Invalid rev format" =>
                    new ResourceOutOfDateException(couchError.ToString(message), ex),
                _ => new ExternalDependencyException(couchError.ToString(message), ex)
            };
        }

        /// <summary>
        /// Couch error response
        /// </summary>
        internal class OperationError
        {
            /// <summary> Error </summary>
            [JsonProperty("error")]
            public string? Error { get; set; }
            /// <summary> Reason </summary>
            [JsonProperty("reason")]
            public string? Reason { get; set; }
            /// <inheritdoc/>
            public string ToString(string? extra)
            {
                var message = $"{Error}: {Reason}";
                if (extra != null)
                {
                    message += $"\n{extra}";
                }
                return message;
            }
        }
    }
}
