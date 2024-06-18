// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Router.Tests
{
    using System;
    using System.Threading.Tasks;

    public sealed class TestExceptionFilterAttribute : ExceptionFilterAttribute
    {
        /// <inheritdoc />
        public override Exception Filter(Exception exception, out int status)
        {
            switch (exception)
            {
                case ArgumentNullException:
                    status = 410;
                    break;
                case TaskCanceledException:
                case OperationCanceledException:
                    status = 4423;
                    return new OperationCanceledException("Operation canceled", exception);
                default:
                    status = 403;
                    break;
            }
            return exception;
        }
    }
}
