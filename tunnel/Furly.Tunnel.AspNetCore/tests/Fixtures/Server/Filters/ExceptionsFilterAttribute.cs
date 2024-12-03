// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Server.Filters
{
    using Furly.Tunnel.Exceptions;
    using Furly.Exceptions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Security;
    using System.Threading.Tasks;

    /// <summary>
    /// Detect all the unhandled exceptions returned by the API controllers
    /// and decorate the response accordingly, managing the HTTP status code
    /// and preparing a JSON response with useful error details.
    /// When including the stack trace, split the text in multiple lines
    /// for an easier parsing.
    /// @see https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/filters
    /// </summary>
    public sealed class ExceptionsFilterAttribute : ExceptionFilterAttribute
    {
        /// <inheritdoc />
        public override void OnException(ExceptionContext context)
        {
            if (context.Exception == null)
            {
                base.OnException(context);
                return;
            }
            if (context.Exception is AggregateException ae)
            {
                var root = ae.GetBaseException();
                if (root is AggregateException && ae.InnerExceptions.Count > 0)
                {
                    context.Exception = ae.InnerExceptions[0];
                }
                else
                {
                    context.Exception = root;
                }
            }
            switch (context.Exception)
            {
                case ResourceNotFoundException:
                    context.Result = GetResponse(HttpStatusCode.NotFound,
                        context.Exception);
                    break;
                case ResourceInvalidStateException:
                    context.Result = GetResponse(HttpStatusCode.Forbidden,
                        context.Exception);
                    break;
                case ResourceConflictException:
                    context.Result = GetResponse(HttpStatusCode.Conflict,
                        context.Exception);
                    break;
                case UnauthorizedAccessException:
                case SecurityException:
                    context.Result = GetResponse(HttpStatusCode.Unauthorized,
                        context.Exception);
                    break;
                case MethodCallStatusException mcs:
                    context.Result = new ObjectResult(mcs.Details);
                    break;
                case SerializerException:
                case MethodCallException:
                case BadRequestException:
                case ArgumentException:
                    context.Result = GetResponse(HttpStatusCode.BadRequest,
                        context.Exception);
                    break;
                case NotImplementedException:
                case NotSupportedException:
                    context.Result = GetResponse(HttpStatusCode.NotImplemented,
                        context.Exception);
                    break;
                case TimeoutException:
                    context.Result = GetResponse(HttpStatusCode.RequestTimeout,
                        context.Exception);
                    break;
                case SocketException:
                case CommunicationException:
                    context.Result = GetResponse(HttpStatusCode.BadGateway,
                        context.Exception);
                    break;

                //
                // The following will most certainly be retried by our
                // service client implementations and thus dependent
                // services:
                //
                //      InternalServerError
                //      BadGateway
                //      ServiceUnavailable
                //      GatewayTimeout
                //      PreconditionFailed
                //      TemporaryRedirect
                //      429 (IoT Hub throttle)
                //
                // As such, if you want to terminate make sure exception
                // is caught ahead of here and returns a status other than
                // one of the above.
                //

                case ResourceOutOfDateException:
                    context.Result = GetResponse(HttpStatusCode.PreconditionFailed,
                        context.Exception);
                    break;
                case ExternalDependencyException:
                    context.Result = GetResponse(HttpStatusCode.ServiceUnavailable,
                        context.Exception);
                    break;
                default:
                    context.Result = GetResponse(HttpStatusCode.InternalServerError,
                        context.Exception);
                    break;
            }
        }

        /// <inheritdoc />
        public override Task OnExceptionAsync(ExceptionContext context)
        {
            try
            {
                OnException(context);
                return Task.CompletedTask;
            }
            catch (Exception)
            {
                return base.OnExceptionAsync(context);
            }
        }

        /// <summary>
        /// Create result
        /// </summary>
        /// <param name="code"></param>
        /// <param name="exception"></param>
        /// <returns></returns>
        private static ObjectResult GetResponse(HttpStatusCode code, Exception exception)
        {
            return new ObjectResult(exception)
            {
                StatusCode = (int)code
            };
        }
    }
}
