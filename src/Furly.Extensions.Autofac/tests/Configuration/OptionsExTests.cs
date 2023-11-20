// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Furly.Extensions.Configuration;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using System.Collections.Generic;
    using System.Threading;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class OptionsExTests
    {
        [Fact]
        public void TestGetOptions()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsConfigureWithCallbacks1()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.Configure<TestOptions>(options => options.Test2 = 1);
            builder.Configure<TestOptions>(options => options.Test1 = "test");
            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test", options.Value.Test1);
            Assert.Equal(1, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsConfigureWithCallbacks2()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.Configure<TestOptions>(options => options.Test2 = 1);
            builder.Configure<TestOptions>(options => options.Test1 = "test");
            builder.Configure<TestOptions>(options => options.Test2 = 0);
            builder.Configure<TestOptions>(options => options.Test1 = null);
            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsConfigureWithClass()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsPostConfigureWithClass()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            builder.RegisterType<TestPostConfigure>().AsImplementedInterfaces();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("", options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsConfigureWithConfig()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("configtest", options.Value.Test1);
            Assert.Equal(55, options.Value.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("configtest", options.Value.Test1);
            Assert.Equal(55, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsConfigureWithConfigAndOverride()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptions<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshot()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotConfigureWithCallbacks1()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.Configure<TestOptions>(options => options.Test2 = 1);
            builder.Configure<TestOptions>(options => options.Test1 = "test");
            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test", options.Value.Test1);
            Assert.Equal(1, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotConfigureWithCallbacks2()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.Configure<TestOptions>(options => options.Test2 = 1);
            builder.Configure<TestOptions>(options => options.Test1 = "test");
            builder.Configure<TestOptions>(options => options.Test2 = 0);
            builder.Configure<TestOptions>(options => options.Test1 = null);
            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotConfigureWithClass()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotPostConfigureWithClass()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            builder.RegisterType<TestPostConfigure>().AsImplementedInterfaces();
            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("", options.Value.Test1);
            Assert.Equal(0, options.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotConfigureWithConfig()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("configtest", options.Value.Test1);
            Assert.Equal(55, options.Value.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("configtest", options.Value.Test1);
            Assert.Equal(55, options.Value.Test2);

            config[nameof(TestOptions.Test1)] = "configtest3";
            config[nameof(TestOptions.Test2)] = "100";
            config.Reload();
            Thread.Sleep(100);

            using var scope2 = scope.BeginLifetimeScope();
            var options2 = scope2.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options2);
            Assert.NotNull(options2.Value);
            Assert.Equal("configtest3", options2.Value.Test1);
            Assert.Equal(100, options2.Value.Test2);
        }

        [Fact]
        public void TestGetOptionsSnapshotConfigureWithConfigAndOverride()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test1000", options.Value.Test1);
            Assert.Equal(1000, options.Value.Test2);
        }

        [Fact]
        public void TestGetNamedOptionsConfigureWithConfig()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "test33",
                [nameof(TestOptions.Test2)] = "33333",
                ["bazkey:" + nameof(TestOptions.Test1)] = "configtest",
                ["bazkey:" + nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Get("bazkey"));
            Assert.Equal("configtest", options.Get("bazkey").Test1);
            Assert.Equal(55, options.Get("bazkey").Test2);

            Assert.NotNull(options);
            Assert.NotNull(options.Value);
            Assert.Equal("test33", options.Value.Test1);
            Assert.Equal(33333, options.Value.Test2);
        }

        [Fact]
        public void TestGetNamedOptionsConfigureWithConfigAndOverride()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();

            var data = new Dictionary<string, string?>
            {
                ["bazkey:" + nameof(TestOptions.Test1)] = "configtest",
                ["bazkey:" + nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Get("bazkey"));
            Assert.Equal("bazkeytest1000", options.Get("bazkey").Test1);
            Assert.Equal(1000, options.Get("bazkey").Test2);
        }

        [Fact]
        public void TestGetNamedOptionsConfigureWithConfigAndPostOverride()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            builder.RegisterType<TestPostConfigure>().AsImplementedInterfaces();

            var data = new Dictionary<string, string?>
            {
                ["bazkey:" + nameof(TestOptions.Test1)] = "configtest",
                ["bazkey:" + nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsSnapshot<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.Get("bazkey"));
            Assert.Equal("", options.Get("bazkey").Test1);
            Assert.Equal(0, options.Get("bazkey").Test2);
        }

        [Fact]
        public void TestGetOptionsMonitorConfigureWithConfig()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsMonitor<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("configtest", options.CurrentValue.Test1);
            Assert.Equal(55, options.CurrentValue.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("configtest2", options.CurrentValue.Test1);
            Assert.Equal(55, options.CurrentValue.Test2);

            config[nameof(TestOptions.Test1)] = "configtest3";
            config[nameof(TestOptions.Test2)] = "100";
            config.Reload();
            Thread.Sleep(100);

            using var scope2 = scope.BeginLifetimeScope();
            var options2 = scope2.Resolve<IOptionsMonitor<TestOptions>>();

            Assert.NotNull(options2);
            Assert.NotNull(options2.CurrentValue);
            Assert.Equal("configtest3", options2.CurrentValue.Test1);
            Assert.Equal(100, options2.CurrentValue.Test2);
        }

        [Fact]
        public void TestGetOptionsMonitorConfigureWithConfigAndOverride()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();

            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsMonitor<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("test1000", options.CurrentValue.Test1);
            Assert.Equal(1000, options.CurrentValue.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("test1000", options.CurrentValue.Test1); // Will be overridden by override
            Assert.Equal(1000, options.CurrentValue.Test2);
        }

        [Fact]
        public void TestGetOptionsMonitorPostConfigureWithClass()
        {
            var builder = new ContainerBuilder();
            builder.AddOptions();
            builder.RegisterType<TestConfigure>().AsImplementedInterfaces();
            builder.RegisterType<TestPostConfigure>().AsImplementedInterfaces();
            var data = new Dictionary<string, string?>
            {
                [nameof(TestOptions.Test1)] = "configtest",
                [nameof(TestOptions.Test2)] = "55"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            builder.AddConfiguration(config);

            using var scope = builder.Build();
            var options = scope.Resolve<IOptionsMonitor<TestOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("", options.CurrentValue.Test1);
            Assert.Equal(0, options.CurrentValue.Test2);

            config[nameof(TestOptions.Test1)] = "configtest2";
            config.Reload();
            Thread.Sleep(100);

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);
            Assert.Equal("", options.CurrentValue.Test1); // Will be overridden by override
            Assert.Equal(0, options.CurrentValue.Test2);
        }
    }

    public class TestOptions
    {
        public string? Test1 { get; set; }
        public int Test2 { get; set; }
    }

    public class TestConfigure : ConfigureOptionBase<TestOptions>
    {
        public TestConfigure(IConfiguration configuration) : base(configuration)
        {
        }

        public override void Configure(string? name, TestOptions options)
        {
            options.Test1 = name + "test1000";
            options.Test2 = 1000;
        }
    }

    public class TestPostConfigure : PostConfigureOptionBase<TestOptions>
    {
        public TestPostConfigure(IConfiguration configuration) : base(configuration)
        {
        }

        public override void PostConfigure(string? name, TestOptions options)
        {
            options.Test1 = "";
            options.Test2 = 0;
        }
    }
}
