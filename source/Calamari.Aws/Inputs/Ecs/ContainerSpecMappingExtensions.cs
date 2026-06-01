using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK.AWS.ECS;
using Amazon.ECS;
using Octopus.Calamari.Contracts.Aws.Ecs;
using LogDriver = Octopus.Calamari.Contracts.Aws.Ecs.LogDriver;

namespace Calamari.Aws.Inputs.Ecs;

public static class ContainerSpecMappingExtensions
{
    
    public static T ConvertedOrDefault<T>(this string value, Func<string, T> converter, Func<T> defaultOverride = null)
    {
        return defaultOverride != null ? string.IsNullOrEmpty(value) ? defaultOverride() : converter(value) : string.IsNullOrEmpty(value) ? default : converter(value);
    }

    public static CfnTaskDefinition.HealthCheckProperty ParseHealthCheck(this ContainerSpec containerSpec)
    {
        if (containerSpec.HealthCheck.Command.Count > 0)
        {
            return new CfnTaskDefinition.HealthCheckProperty
            {
                Command = containerSpec.HealthCheck.Command.ToArray(),
                Interval = containerSpec.HealthCheck.Interval.ConvertedOrDefault<double?>(s => double.Parse(s)),
                Retries = containerSpec.HealthCheck.Retries.ConvertedOrDefault<double?>(s => double.Parse(s)),
                StartPeriod = containerSpec.HealthCheck.StartPeriod.ConvertedOrDefault<double?>(s => double.Parse(s)),
                Timeout = containerSpec.HealthCheck.Timeout.ConvertedOrDefault<double?>(s => double.Parse(s)),
            };
        }
        return null;
    }

    public static Dictionary<string, string> ParseDockerLabels(this ContainerSpec containerSpec)
    {
        // Grouping Handle potential duplicates
        var dockerLabels =  containerSpec.DockerLabels
                                                  .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.Last().Value);
        return dockerLabels.Count == 0 ? null : dockerLabels;

    }

    public static CfnTaskDefinition.KeyValuePairProperty[] ParseEnvironmentVariables(this ContainerSpec containerSpec)
    {
        var environmentVariables = containerSpec.EnvironmentVariables
                            .Where(tkp => tkp.Type == KeyValueType.Plain)
                            .GroupBy(kvp => kvp.Key)
                            .Select(g => new CfnTaskDefinition.KeyValuePairProperty
                            {
                                Name = g.Key,
                                Value = g.Last().Value,
                            })
                            .ToArray();
        
        return environmentVariables.Length == 0 ? null : environmentVariables;
        
    }

    public static CfnTaskDefinition.PortMappingProperty[] ParsePortMappings(this ContainerSpec containerSpec)
    {
        return containerSpec.ContainerPortMappings.Select(pm => new CfnTaskDefinition.PortMappingProperty
                                    {
                                        ContainerPort = pm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s)),
                                        HostPort = pm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s)),
                                        Protocol = pm.Protocol.ToString().ToLower(),

                                    })
                                    .ToArray();
    }

    public static CfnTaskDefinition.HostEntryProperty[] ParseExtraHosts(this ContainerSpec containerSpec)
    {
        return containerSpec.NetworkSettings.ExtraHosts.Select(eh => new CfnTaskDefinition.HostEntryProperty
        {
            Hostname = string.IsNullOrEmpty(eh.Hostname) ? null : eh.Hostname,
            IpAddress = string.IsNullOrEmpty(eh.IpAddress) ? null : eh.IpAddress,
        }).ToArray();
    }

    public static CfnTaskDefinition.RepositoryCredentialsProperty ParseRepositoryCredentials(this ContainerSpec containerSpec)
    {
        return containerSpec.RepositoryAuthentication.Type switch
               {
                   RepositoryAuthenticationType.Default => null,
                   _ => new CfnTaskDefinition.RepositoryCredentialsProperty
                   {
                       CredentialsParameter = containerSpec.RepositoryAuthentication.SecretName
                   }
               };
    }

    public static CfnTaskDefinition.ResourceRequirementProperty[] ParseResourceRequirements(this ContainerSpec containerSpec)
    {
        return string.IsNullOrEmpty(containerSpec.Gpus)
            ? []
            : [new CfnTaskDefinition.ResourceRequirementProperty
            {
                Type = ResourceType.GPU,
                Value = containerSpec.Gpus,
            }];
    }

    public static  CfnTaskDefinition.UlimitProperty[] ParseULimits(this ContainerSpec containerSpec)
    {
        if (containerSpec.Ulimits.Count > 0)
        {
            return containerSpec.Ulimits.Select(ul => new CfnTaskDefinition.UlimitProperty
            {
                Name = new Amazon.ECS.UlimitName(ul.LimitName),
                HardLimit = double.Parse(ul.HardLimit),
                SoftLimit = double.Parse(ul.SoftLimit),

            }).ToArray();
        }

        return null;
    }

    public static CfnTaskDefinition.MountPointProperty[] ParseMountPoints(this ContainerSpec containerSpec)
    {
        if (containerSpec.ContainerStorage.MountPoints.Count > 0)
        {
            return containerSpec.ContainerStorage.MountPoints.Select(mp => new CfnTaskDefinition.MountPointProperty
                                {
                                    SourceVolume =  string.IsNullOrEmpty(mp.SourceVolume) ? null : mp.SourceVolume,
                                    ContainerPath =   string.IsNullOrEmpty(mp.ContainerPath) ? null : mp.ContainerPath,
                                    ReadOnly = mp.Readonly.ConvertedOrDefault(bool.Parse)
                                })
                         .ToArray();
        }

        return null;
    }

    public static CfnTaskDefinition.ContainerDependencyProperty[] ParseDependencies(this ContainerSpec containerSpec)
    {
        if (containerSpec.Dependencies.Count > 0)
        {
            return containerSpec.Dependencies.Select(d => new CfnTaskDefinition.ContainerDependencyProperty
            {
                ContainerName = string.IsNullOrEmpty(d.ContainerName) ? null : d.ContainerName,
                Condition = d.Condition.ToString().ToUpperInvariant(),
            }).ToArray();
        }

        return null;
    }
    
    public static CfnTaskDefinition.VolumeFromProperty[] ParseVolumesFrom(this ContainerSpec containerSpec)
    {
        if (containerSpec.ContainerStorage.VolumeFrom.Count > 0)
        {
            return containerSpec.ContainerStorage.VolumeFrom.Select(vf => new CfnTaskDefinition.VolumeFromProperty
            {
                SourceContainer = string.IsNullOrEmpty(vf.SourceContainer) ? null : vf.SourceContainer,
                ReadOnly = vf.Readonly.ConvertedOrDefault(bool.Parse)
            }).ToArray();
        }

        return null;
    }

    public static CfnTaskDefinition.EnvironmentFileProperty[] ParseEnvironmentFiles(this ContainerSpec containerSpec)
    {
        if (containerSpec.EnvironmentFiles.Count > 0)
        {
            return containerSpec.EnvironmentFiles.Select(ef => new CfnTaskDefinition.EnvironmentFileProperty
            {
                Type = "s3", // Hardcoded to always be S3 until we support other options
                Value = ef
                
            }).ToArray();
        }

        return [];
    }

    public static CfnTaskDefinition.LogConfigurationProperty ParseLogConfiguration(
        this ContainerSpec containerSpec,
        string logGroupNameRef,
        string awsRegionRef)
    {
        switch (containerSpec.ContainerLogging.Type)
        {
            case ContainerLoggingType.Auto:
                // Auto = "wire it up for me". LogDriver/LogOptions on the spec are ignored;
                // we emit the standard awslogs configuration pointing at the task's log group.
                return new CfnTaskDefinition.LogConfigurationProperty
                {
                    LogDriver = LogDriver.AwsLogs.ToString().ToLowerInvariant(),
                    Options = new Dictionary<string, string>
                    {
                        { "awslogs-group",         logGroupNameRef },
                        { "awslogs-region",        awsRegionRef },
                        { "awslogs-stream-prefix", "ecs" }
                    }
                };

            case ContainerLoggingType.Manual:
            default:
                // Manual: honour the user's chosen driver and options. None / unset = no log config.
                if (!containerSpec.ContainerLogging.LogDriver.HasValue
                    || containerSpec.ContainerLogging.LogDriver is LogDriver.None)
                {
                    return null;
                }

                return new CfnTaskDefinition.LogConfigurationProperty
                {
                    LogDriver = containerSpec.ContainerLogging.LogDriver.Value.ToString().ToLowerInvariant(),
                    Options = containerSpec.ContainerLogging.LogOptions
                                           .Where(lo => lo.Type == KeyValueType.Plain)
                                           .ToDictionary(opt => opt.Key, opt => opt.Value),
                    SecretOptions = containerSpec.ContainerLogging.LogOptions
                                                 .Where(lo => lo.Type == KeyValueType.Secret)
                                                 .ToDictionary(opt => opt.Key, opt => opt.Value),
                };
        }
    }

    public static CfnTaskDefinition.FirelensConfigurationProperty ParseFireLensConfiguration(this ContainerSpec containerSpec)
    {
        if (containerSpec.FirelensConfiguration.Type == FireLensConfigurationType.Disabled)
        {
            return null;
        }

        var options = new Dictionary<string, string>
        {
            { "enable-ecs-log-metadata", containerSpec.FirelensConfiguration.EnableEcsLogMetadata.ToLowerInvariant() }
        };
        if (containerSpec.FirelensConfiguration.CustomConfigSource is { Type: not FireLensCustomConfigSourceType.None })
        {
            options.Add("config-file-type", containerSpec.FirelensConfiguration.CustomConfigSource.Type.ToString().ToLowerInvariant());
            options.Add("config-file-value", containerSpec.FirelensConfiguration.CustomConfigSource.FilePath);
        }
        var fireLensConfig =  new CfnTaskDefinition.FirelensConfigurationProperty
        {
            Type = containerSpec.FirelensConfiguration.FirelensType.ToString()?.ToLowerInvariant(),
            Options = options
           
        };

        return fireLensConfig;
    }

    public static CfnTaskDefinition.SecretProperty[] ParseSecrets(this ContainerSpec containerSpec)
    {
        var secrets =  containerSpec.EnvironmentVariables
                            .Where(tkp => tkp.Type == KeyValueType.Secret)
                            .GroupBy(kvp => kvp.Key) // Dedupe
                            .Select(g => new CfnTaskDefinition.SecretProperty()
                            {
                                Name = g.Key,
                                ValueFrom = g.Last().Value
                            })
                            .ToArray();
        
        return secrets.Length > 0 ? secrets : null;
    }
}