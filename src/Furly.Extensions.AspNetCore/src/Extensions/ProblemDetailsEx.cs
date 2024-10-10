// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Http
{
    using Microsoft.AspNetCore.Mvc;
    using Furly.Exceptions;
    using System;

    /// <summary>
    /// Get as problem details
    /// </summary>
    public static class ProblemDetailsEx
    {
        /// <summary>
        /// Convert to problem details
        /// </summary>
        /// <param name="problem"></param>
        /// <returns></returns>
        public static ProblemDetails ToProblemDetails(this ErrorDetails problem)
        {
            ArgumentNullException.ThrowIfNull(problem);
            return new ProblemDetails
            {
                Title = problem.Title,
                Status = problem.Status,
                Detail = problem.Detail,
                Instance = problem.Instance,
                Type = problem.Type,
                Extensions = problem.Extensions
            };
        }

        /// <summary>
        /// Convert to problem details
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static ProblemDetails ToProblemDetails(this MethodCallStatusException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            return ex.Details.ToProblemDetails();
        }
    }
}
