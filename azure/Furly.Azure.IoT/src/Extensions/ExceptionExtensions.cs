// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Azure.Devices.Common.Exceptions
{
    using Furly.Exceptions;
    using System;

    /// <summary>
    /// IoT Hub exception extension
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Translate exception
        /// </summary>
        internal static Exception Translate(this Exception ex)
        {
            switch (ex)
            {
                case ModuleNotFoundException mex:
                    return new ResourceNotFoundException(mex.Message, mex);
                case DeviceNotFoundException dex:
                    return new ResourceNotFoundException(dex.Message, dex);
                case DeviceAlreadyExistsException aex:
                    return new ResourceConflictException(aex.Message, aex);
                case ModuleAlreadyExistsException max:
                    return new ResourceConflictException(max.Message, max);
                case ConfigurationNotFoundException cex:
                    return new ResourceNotFoundException(cex.Message, cex);
                case JobNotFoundException jex:
                    return new ResourceNotFoundException(jex.Message, jex);
                case IotHubNotFoundException iex:
                    return new ResourceNotFoundException(iex.Message, iex);
                case UnauthorizedException ue:
                    return new UnauthorizedAccessException(ue.Message, ue);
                case MessageTooLargeException mtl:
                    return new MessageSizeLimitException(mtl.Message);
                case DeviceMessageLockLostException mtl:
                    return new BadRequestException(mtl.Message, mtl);
                case TooManyModulesOnDeviceException tmd:
                    return new BadRequestException(tmd.Message, tmd);
                case PreconditionFailedException pf:
                    return new ResourceOutOfDateException(pf.Message, pf);
                case JobQuotaExceededException qe:
                    return new ResourceInvalidStateException(qe.Message, qe);
                case QuotaExceededException qe:
                    return new ResourceInvalidStateException(qe.Message, qe);
                case ServerErrorException se:
                    return new ResourceInvalidStateException(se.Message, se);
                //TODO  case ServerBusyException sb:
                //TODO      return new HttpTransientException(HttpStatusCode.ServiceUnavailable, sb.Message);
                //TODO  case IotHubThrottledException te:
                //TODO      return new HttpTransientException((HttpStatusCode)429, te.Message);
                //TODO  case ProvisioningServiceClientTransportException ptx:
                //TODO      return new HttpTransientException(ptx.Message, ptx);
                default:
                    return ex;
            }
        }
    }
}
