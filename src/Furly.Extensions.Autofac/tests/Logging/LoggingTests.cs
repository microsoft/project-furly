// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Logging
{
    using Autofac;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class LoggingTests
    {
        [Fact]
        public void ResolveLoggerInServiceTest()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<LoggingModule>();
            builder.RegisterType<Test>().AsSelf();

            using var container = builder.Build();
            var test = container.Resolve<Test>();

            Assert.NotNull(test);
        }

        [Fact]
        public void ResolveLoggerDirect1Test()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<LoggingModule>();
            builder.RegisterType<Test>().AsSelf();

            using var container = builder.Build();
            var logger = container.Resolve<ILogger<Test>>();

            Assert.NotNull(logger);
            Assert.True(logger is ILogger<Test>);
        }

        [Fact]
        public void ResolveLoggerDirect2Test()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<LoggingModule>();
            builder.RegisterType<Test>().AsSelf();

            using var container = builder.Build();
            var logger = container.Resolve<ILogger>();

            Assert.NotNull(logger);
            Assert.True(logger is ILogger<LoggingModule>);
        }

#pragma warning disable CA1034 // Nested types should not be visible
        public class Test
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public Test(ILogger logger)
            {
                Assert.NotNull(logger);
                Assert.True(logger is ILogger<Test>);
            }
        }
    }
}
