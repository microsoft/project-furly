// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Exceptions.Tests
{
    using Furly.Exceptions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.ExceptionSummarization;
    using System;
    using Xunit;

    public class ExceptionSummarizationTests
    {
        [Fact]
        public void SummarizeResourceNotFoundException()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddExceptionSummarization();
            using var provider = serviceCollection.BuildServiceProvider();
            var summarizer = provider.GetRequiredService<IExceptionSummarizer>();
            var summary = summarizer.Summarize(new ResourceNotFoundException("This is a test"));
            Assert.Equal("ResourceNotFoundException", summary.ExceptionType);
            Assert.Equal("This is a test", summary.AdditionalDetails);
            Assert.Equal("The requested resource could not be found.", summary.Description);
        }

        [Fact]
        public void SummarizeOperationCancelledException()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddExceptionSummarization();
            using var provider = serviceCollection.BuildServiceProvider();
            var summarizer = provider.GetRequiredService<IExceptionSummarizer>();
            var summary = summarizer.Summarize(new OperationCanceledException());
            Assert.Equal("OperationCanceledException", summary.ExceptionType);
            Assert.Equal("Reason unknown", summary.AdditionalDetails);
            Assert.Equal("The operation was cancelled by the system or due to user action.",
                summary.Description);
        }
    }
}
