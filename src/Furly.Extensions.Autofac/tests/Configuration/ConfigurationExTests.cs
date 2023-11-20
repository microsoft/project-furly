// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Autofac
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Configuration.Memory;
    using System;
    using System.Collections.Generic;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class ConfigurationExTests
    {
        [Fact]
        public void TestGetFromRootConfiguration()
        {
            var c = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST"] = "test"
                })
                .Build();
            var builder = new ContainerBuilder().AddConfiguration(c);

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal("test", value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithOverride1()
        {
            Environment.SetEnvironmentVariable("TEST", "other");
            var c = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST"] = "test"
                })
                .Build();
            var builder = new ContainerBuilder()
                .AddConfiguration(c)
                .AddEnvironmentVariableConfiguration(); // Will be found here

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal("other", value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithOverride2()
        {
            Environment.SetEnvironmentVariable("TEST", "other");
            var c = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST"] = "test"
                })
                .Build();
            var builder = new ContainerBuilder()
                .AddConfiguration(c) // Will be found here
                .AddEnvironmentVariableConfiguration(ConfigSourcePriority.Low);

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal("test", value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithOverride3()
        {
            Environment.SetEnvironmentVariable("TEST", "other");
            var builder = new ContainerBuilder()
                .AddEnvironmentVariableConfiguration();
            var c = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST"] = "test"
                })
                .Build();
            builder = builder.AddConfiguration(c); // Will be found here

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal("test", value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithCustomSource0()
        {
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfiguration(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST"] = "test"
                })
                .Build()); // Found here

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal("test", value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithCustomSource1()
        {
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>();

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Double), value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithCustomSource2()
        {
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>() // This is the last
                .AddConfigurationSource<CustomSource<double>>(ConfigSourcePriority.Low);

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(String), value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithCustomSource3()
        {
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<double>>(ConfigSourcePriority.Low)
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>(); // This is the last

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(String), value);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration1()
        {
            var capturedValue = "None";
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue = configuration.GetValue<string>("TEST");
                  return new CustomSource<bool>();
              }); // last item should now resolve

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Boolean), value);
            Assert.Equal(nameof(Double), capturedValue);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration2()
        {
            var capturedValue = "None";
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue = configuration.GetValue<string>("TEST");
                  return new CustomSource<bool>();
              }, ConfigSourcePriority.Low); // Will be overridden

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Double), value);
            Assert.Equal(nameof(Double), capturedValue);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration2b()
        {
            var capturedValue1 = "None";
            var capturedValue2 = "None";
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue1 = configuration.GetValue<string>("TEST");
                  return new CustomSource<bool>();
              })
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue2 = configuration.GetValue<string>("TEST");
                  return new CustomSource<short>();
              }, ConfigSourcePriority.Low);

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Boolean), value);
            Assert.Equal(nameof(Double), capturedValue1);
            Assert.Equal(nameof(Boolean), capturedValue2);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration2c()
        {
            var capturedValue1 = "None";
            var capturedValue2 = "None";
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue1 = configuration.GetValue<string>("TEST");
                  return new CustomSource<bool>();
              }, ConfigSourcePriority.Low)
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue2 = configuration.GetValue<string>("TEST");
                  return new CustomSource<short>();
              }, ConfigSourcePriority.Low);

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Double), value);
            Assert.Equal(nameof(Double), capturedValue1);
            Assert.Equal(nameof(Double), capturedValue2);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration2d()
        {
            var capturedValue1 = "None";
            var capturedValue2 = "None";

            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue1 = configuration.GetValue<string>("TEST");
                  return new CustomSource<bool>();
              }, ConfigSourcePriority.Low)
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
              {
                  // Capture value and then return bool source
                  capturedValue2 = configuration.GetValue<string>("TEST");
                  return new CustomSource<short>();
              });

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Int16), value);
            Assert.Equal(nameof(Double), capturedValue1);
            Assert.Equal(nameof(Double), capturedValue2);
        }

        [Fact]
        public void TestGetFromRootConfigurationWithPreBuiltConfiguration3()
        {
            var capturedValue = "None";
            var builder = new ContainerBuilder()
                .AddConfigurationSource<CustomSource<int>>()
                .AddConfigurationSource<CustomSource<string>>()
                .AddConfigurationSource<CustomSource<double>>()
                .AddConfigurationSource(configuration =>
                {
                    // Capture value and then return bool source
                    capturedValue = configuration.GetValue<string>("TEST");
                    return null;
                });

            using var scope = builder.Build();
            var confguration = scope.Resolve<IConfiguration>();
            var root = scope.Resolve<IConfigurationRoot>();
            Assert.NotNull(root);
            Assert.NotNull(confguration);

            var value = confguration.GetValue<string>("TEST");
            Assert.Equal(nameof(Double), value);
            Assert.Equal(nameof(Double), capturedValue);
        }

        internal sealed class CustomSource<T> : MemoryConfigurationSource
        {
            public CustomSource()
            {
                InitialData = new Dictionary<string, string?>
                {
                    ["TEST"] = typeof(T).Name
                };
            }
            public CustomSource(string overrider)
            {
                InitialData = new Dictionary<string, string?>
                {
                    ["TEST"] = overrider
                };
            }
        }
    }
}
