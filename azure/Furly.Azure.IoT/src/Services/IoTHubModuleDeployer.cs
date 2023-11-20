// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Services
{
    using Furly.Extensions.Serializers;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Deploys modules according to a layered deployment description
    /// </summary>
    public sealed class IoTHubModuleDeployer : IAwaitable<IoTHubModuleDeployer>
    {
        /// <summary>
        /// Create deployer
        /// </summary>
        /// <param name="options"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="deployments"></param>
        public IoTHubModuleDeployer(IOptions<IoTHubServiceOptions> options,
            IJsonSerializer serializer, ILogger<IoTHubModuleDeployer> logger,
            IEnumerable<IIoTEdgeDeployment> deployments)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _layers = deployments ?? throw new ArgumentNullException(nameof(deployments));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (string.IsNullOrEmpty(options.Value.ConnectionString) ||
                !ConnectionString.TryParse(options.Value.ConnectionString, out var cs) ||
                cs.HostName == null)
            {
                throw new ArgumentException("Missing or bad connection string", nameof(options));
            }

            _deployment = DeployAsync(options.Value.ConnectionString);
        }

        /// <inheritdoc/>
        public IAwaiter<IoTHubModuleDeployer> GetAwaiter()
        {
            return _deployment.AsAwaiter(this);
        }

        /// <summary>
        /// Run the deployment
        /// </summary>
        /// <returns></returns>
        private async Task DeployAsync(string connectionString)
        {
            using var registry = RegistryManager.CreateFromConnectionString(connectionString);
            await registry.OpenAsync().ConfigureAwait(false);

            // Apply layered configuration
            var baseLayersApplied = new HashSet<string>();
            foreach (var layer in _layers)
            {
                if (!string.IsNullOrEmpty(layer.BaseDeploymentId) &&
                    !baseLayersApplied.Contains(layer.BaseDeploymentId))
                {
                    // Apply automatic configuration
                    var baseConfiguration = new Configuration(layer.BaseDeploymentId)
                    {
                        Content = new ConfigurationContent
                        {
                            ModulesContent = GetBaseLayer("1.4")
                        },
                        TargetCondition = layer.BaseTargetCondition ?? layer.TargetCondition,
                        Priority = 0
                    };

                    await AddOrUpdateAsync(registry, baseConfiguration).ConfigureAwait(false);
                    baseLayersApplied.Add(layer.BaseDeploymentId);
                }

                // Apply layer
                var configuration = new Configuration(layer.Id)
                {
                    Content = new ConfigurationContent
                    {
                        ModulesContent = CreateLayeredDeployment(layer)
                    },
                    TargetCondition = layer.TargetCondition,
                    Priority = layer.Priority
                };

                await AddOrUpdateAsync(registry, configuration).ConfigureAwait(false);
            }

            static async Task AddOrUpdateAsync(RegistryManager registry, Configuration configuration)
            {
                try
                {
                    await registry.UpdateConfigurationAsync(configuration, true).ConfigureAwait(false);
                }
                catch (ConfigurationNotFoundException)
                {
                    await registry.AddConfigurationAsync(configuration).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Get base edge configuration
        /// </summary>
        /// <returns></returns>
        private IDictionary<string, IDictionary<string, object>> CreateLayeredDeployment(
            IIoTEdgeDeployment deployment)
        {
            var registryCredentials = "";
            if (!string.IsNullOrEmpty(deployment.DockerServer) &&
                deployment.DockerServer != "mcr.microsoft.com")
            {
                var registryId = deployment.DockerServer.Split('.')[0];
                registryCredentials = @"
                    ""properties.desired.runtime.settings.registryCredentials." + registryId + @""": {
                        ""address"": """ + deployment.DockerServer + @""",
                        ""password"": """ + deployment.DockerPassword + @""",
                        ""username"": """ + deployment.DockerUser + @"""
                    },
                ";
            }

            var createOptions = _serializer.SerializeToString(deployment.CreateOptions);
            var server = string.IsNullOrEmpty(deployment.DockerServer) ?
                "mcr.microsoft.com" : deployment.DockerServer;
            var version = deployment.Tag ?? "latest";
            var image = $"{server}/{deployment.Image}:{version}";
            var moduleName = deployment.ModuleName ?? deployment.Image;

            _logger.LogInformation("Deployment {Image}", image);

            // Return deployment modules object
            var content = @"
            {
                ""$edgeAgent"": {
                    " + registryCredentials + @"
                    ""properties.desired.modules." + moduleName + @""": {
                        ""settings"": {
                            ""image"": """ + image + @""",
                            ""createOptions"":" + createOptions + @"
                        },
                        ""type"": ""docker"",
                        ""status"": ""running"",
                        ""restartPolicy"": ""always"",
                        ""version"": """ + (version == "latest" ? "1.0" : version) + @"""
                    }
                },
                ""$edgeHub"": {
                    ""properties.desired.routes.upstream"": ""FROM /messages/* INTO $upstream""
                }
            }";
            return _serializer.Deserialize<IDictionary<string, IDictionary<string, object>>>(content)!;
        }

        /// <summary>
        /// Get base edge configuration
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private IDictionary<string, IDictionary<string, object>>? GetBaseLayer(string version)
        {
            return _serializer.Deserialize<IDictionary<string, IDictionary<string, object>>>(@"
{
    ""$edgeAgent"": {
        ""properties.desired"": {
            ""schemaVersion"": """ + kDefaultSchemaVersion + @""",
            ""runtime"": {
                ""type"": ""docker"",
                ""settings"": {
                    ""minDockerVersion"": ""v1.25"",
                    ""loggingOptions"": """",
                    ""registryCredentials"": {
                    }
                }
            },
            ""systemModules"": {
                ""edgeAgent"": {
                    ""type"": ""docker"",
                    ""settings"": {
                        ""image"": ""mcr.microsoft.com/azureiotedge-agent:" + version + @""",
                        ""createOptions"": ""{}""
                    },
                    ""env"": {
                        ""ExperimentalFeatures__Enabled"": {
                            ""value"": ""true""
                        },
                        ""ExperimentalFeatures__EnableGetLogs"": {
                            ""value"": ""true""
                        },
                        ""ExperimentalFeatures__EnableUploadLogs"": {
                            ""value"": ""true""
                        },
                        ""ExperimentalFeatures__EnableMetrics"": {
                            ""value"": ""true""
                        }
                    }
                },
                ""edgeHub"": {
                    ""type"": ""docker"",
                    ""status"": ""running"",
                    ""restartPolicy"": ""always"",
                    ""settings"": {
                        ""image"": ""mcr.microsoft.com/azureiotedge-hub:" + version + @""",
                        ""createOptions"": ""{\""HostConfig\"":{\""PortBindings\"":{\""443/tcp\"":[{\""HostPort\"":\""443\""}],\""5671/tcp\"":[{\""HostPort\"":\""5671\""}],\""8883/tcp\"":[{\""HostPort\"":\""8883\""}]}},\""ExposedPorts\"":{\""5671/tcp\"":{},\""8883/tcp\"":{}}}""
                    },
                    ""env"": {
                        ""SslProtocols"": {
                            ""value"": ""tls1.2""
                        }
                    }
                }
            },
            ""modules"": {
            }
        }
    },
    ""$edgeHub"": {
        ""properties.desired"": {
            ""schemaVersion"": """ + kDefaultSchemaVersion + @""",
            ""storeAndForwardConfiguration"": {
                ""timeToLiveSecs"": 7200
            },
            ""routes"" : {
            }
        }
    }
}
");
        }

        private const string kDefaultSchemaVersion = "1.1";
        private readonly IJsonSerializer _serializer;
        private readonly ILogger _logger;
        private readonly IEnumerable<IIoTEdgeDeployment> _layers;
        private readonly Task _deployment;
    }
}
