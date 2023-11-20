// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Hosting.Runtime
{
    using Furly.Extensions.Configuration;
    using Furly.Extensions.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.HttpsPolicy;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Web Host configuration
    /// </summary>
    internal sealed class WebHostConfig : PostConfigureOptionBase<WebHostOptions>,
        IConfigureOptions<HttpsRedirectionOptions>,
        IConfigureNamedOptions<HttpsRedirectionOptions>
    {
        /// <inheritdoc/>
        public WebHostConfig(IConfiguration configuration) :
            base(configuration)
        {
        }

        /// <inheritdoc/>
        public override void PostConfigure(string? name, WebHostOptions options)
        {
            options.UseHttpsRedirect = GetIntOrNull(
                EnvironmentVariable.HTTPSREDIRECTPORT) != null;
        }

        /// <inheritdoc/>
        public void Configure(string? name, HttpsRedirectionOptions options)
        {
            options.HttpsPort = GetIntOrNull(
                EnvironmentVariable.HTTPSREDIRECTPORT, options.HttpsPort);
            options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
        }

        /// <inheritdoc/>
        public void Configure(HttpsRedirectionOptions options)
        {
            Configure(Options.DefaultName, options);
        }
    }
}
