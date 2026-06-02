using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Cfn = Calamari.Aws.Integration.Ecs.Deploy.Cfn;
using LogDriver = Octopus.Calamari.Contracts.Aws.Ecs.LogDriver;

namespace Calamari.Aws.Inputs.Ecs;

public static class ContainerSpecMappingExtensions
{
    public static T ConvertedOrDefault<T>(this string value, Func<string, T> converter, Func<T> defaultOverride = null)
    {
        return defaultOverride != null
            ? string.IsNullOrEmpty(value) ? defaultOverride() : converter(value)
            : string.IsNullOrEmpty(value)
                ? default
                : converter(value);
    }

    public static Cfn.HealthCheck ParseHealthCheck(this ContainerSpec containerSpec)
    {
        if (containerSpec.HealthCheck.Command.Count == 0) return null;

        return new Cfn.HealthCheck
        {
            Command = containerSpec.HealthCheck.Command.ToArray(),
            Interval = containerSpec.HealthCheck.Interval.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
            Retries = containerSpec.HealthCheck.Retries.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
            StartPeriod = containerSpec.HealthCheck.StartPeriod.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
            Timeout = containerSpec.HealthCheck.Timeout.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
        };
    }

    public static Dictionary<string, string> ParseDockerLabels(this ContainerSpec containerSpec)
    {
        var labels = containerSpec.DockerLabels
                                  .GroupBy(kvp => kvp.Key)
                                  .ToDictionary(g => g.Key, g => g.Last().Value);
        return labels.Count == 0 ? null : labels;
    }

    public static Cfn.EnvironmentEntry[] ParseEnvironmentVariables(this ContainerSpec containerSpec)
    {
        var entries = containerSpec.EnvironmentVariables
                                   .Where(tkp => tkp.Type == KeyValueType.Plain)
                                   .GroupBy(kvp => kvp.Key)
                                   .Select(g => new Cfn.EnvironmentEntry { Name = g.Key, Value = g.Last().Value })
                                   .ToArray();
        return entries.Length == 0 ? null : entries;
    }

    // SPF always emits PortMappings as an array — empty becomes [] not omitted.
    public static Cfn.PortMapping[] ParsePortMappings(this ContainerSpec containerSpec) =>
        containerSpec.ContainerPortMappings.Select(pm => new Cfn.PortMapping
                     {
                         ContainerPort = pm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s, CultureInfo.InvariantCulture)),
                         HostPort = pm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s, CultureInfo.InvariantCulture)),
                         Protocol = pm.Protocol.ToString().ToLowerInvariant()
                     })
                     .ToArray();

    // SPF always emits ExtraHosts as an array — empty becomes [] not omitted.
    public static Cfn.ExtraHost[] ParseExtraHosts(this ContainerSpec containerSpec) =>
        containerSpec.NetworkSettings.ExtraHosts.Select(eh => new Cfn.ExtraHost
                     {
                         Hostname = string.IsNullOrEmpty(eh.Hostname) ? null : eh.Hostname,
                         IpAddress = string.IsNullOrEmpty(eh.IpAddress) ? null : eh.IpAddress,
                     })
                     .ToArray();

    public static Cfn.RepositoryCredentials ParseRepositoryCredentials(this ContainerSpec containerSpec) =>
        containerSpec.RepositoryAuthentication.Type switch
        {
            RepositoryAuthenticationType.Default => null,
            _ => new Cfn.RepositoryCredentials
            {
                CredentialsParameter = containerSpec.RepositoryAuthentication.SecretName
            }
        };

    // SPF always emits ResourceRequirements as an array — empty becomes [] not omitted.
    public static Cfn.ResourceRequirement[] ParseResourceRequirements(this ContainerSpec containerSpec) =>
        string.IsNullOrEmpty(containerSpec.Gpus)
            ? []
            : [new Cfn.ResourceRequirement { Type = "GPU", Value = containerSpec.Gpus }];

    public static Cfn.Ulimit[] ParseULimits(this ContainerSpec containerSpec)
    {
        if (containerSpec.Ulimits.Count == 0) return null;

        return containerSpec.Ulimits.Select(ul => new Cfn.Ulimit
                            {
                                Name = ul.LimitName,
                                HardLimit = double.Parse(ul.HardLimit, CultureInfo.InvariantCulture),
                                SoftLimit = double.Parse(ul.SoftLimit, CultureInfo.InvariantCulture)
                            })
                            .ToArray();
    }

    public static Cfn.MountPoint[] ParseMountPoints(this ContainerSpec containerSpec)
    {
        if (containerSpec.ContainerStorage.MountPoints.Count == 0) return null;

        return containerSpec.ContainerStorage.MountPoints.Select(mp => new Cfn.MountPoint
                            {
                                SourceVolume = string.IsNullOrEmpty(mp.SourceVolume) ? null : mp.SourceVolume,
                                ContainerPath = string.IsNullOrEmpty(mp.ContainerPath) ? null : mp.ContainerPath,
                                ReadOnly = mp.Readonly.ConvertedOrDefault<bool?>(s => bool.Parse(s))
                            })
                            .ToArray();
    }

    public static Cfn.ContainerDependency[] ParseDependencies(this ContainerSpec containerSpec)
    {
        if (containerSpec.Dependencies.Count == 0) return null;

        return containerSpec.Dependencies.Select(d => new Cfn.ContainerDependency
                            {
                                ContainerName = string.IsNullOrEmpty(d.ContainerName) ? null : d.ContainerName,
                                Condition = d.Condition.ToString().ToUpperInvariant(),
                            })
                            .ToArray();
    }

    public static Cfn.VolumeFrom[] ParseVolumesFrom(this ContainerSpec containerSpec)
    {
        if (containerSpec.ContainerStorage.VolumeFrom.Count == 0) return null;

        return containerSpec.ContainerStorage.VolumeFrom.Select(vf => new Cfn.VolumeFrom
                            {
                                SourceContainer = string.IsNullOrEmpty(vf.SourceContainer) ? null : vf.SourceContainer,
                                ReadOnly = vf.Readonly.ConvertedOrDefault<bool?>(s => bool.Parse(s))
                            })
                            .ToArray();
    }

    // SPF always emits EnvironmentFiles as an array — empty becomes [] not omitted.
    public static Cfn.EnvironmentFile[] ParseEnvironmentFiles(this ContainerSpec containerSpec) =>
        containerSpec.EnvironmentFiles.Select(ef => new Cfn.EnvironmentFile
                     {
                         Type = "s3", // Hardcoded until we support other options
                         Value = ef
                     })
                     .ToArray();

    // logGroupNameRef and awsRegionRef are passed as Cfn.Value<string> so callers can hand
    // in either a literal or a Ref intrinsic. The Auto path consumes them; Manual ignores.
    public static Cfn.LogConfiguration ParseLogConfiguration(
        this ContainerSpec containerSpec,
        Cfn.Value<string> logGroupNameRef,
        Cfn.Value<string> awsRegionRef)
    {
        switch (containerSpec.ContainerLogging.Type)
        {
            case ContainerLoggingType.Auto:
                // Auto = "wire it up for me". LogDriver/LogOptions on the spec are ignored;
                // emit the standard awslogs configuration pointing at the task's log group.
                return new Cfn.LogConfiguration
                {
                    LogDriver = LogDriver.AwsLogs.ToString().ToLowerInvariant(),
                    Options = new Dictionary<string, Cfn.Value<string>>
                    {
                        { "awslogs-group", logGroupNameRef },
                        { "awslogs-region", awsRegionRef },
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

                return new Cfn.LogConfiguration
                {
                    LogDriver = containerSpec.ContainerLogging.LogDriver.Value.ToString().ToLowerInvariant(),
                    Options = containerSpec.ContainerLogging.LogOptions
                                           .Where(o => o.Type == KeyValueType.Plain)
                                           .ToDictionary<TypedKeyValuePair, string, Cfn.Value<string>>(
                                               o => o.Key,
                                               o => o.Value),
                    SecretOptions = containerSpec.ContainerLogging.LogOptions
                                                 .Where(o => o.Type == KeyValueType.Secret)
                                                 .Select(o => new Cfn.Secret { Name = o.Key, ValueFrom = o.Value })
                                                 .ToArray()
                };
        }
    }

    public static Cfn.FirelensConfiguration ParseFireLensConfiguration(this ContainerSpec containerSpec)
    {
        if (containerSpec.FirelensConfiguration.Type == FireLensConfigurationType.Disabled) return null;

        var options = new Dictionary<string, string>
        {
            { "enable-ecs-log-metadata", containerSpec.FirelensConfiguration.EnableEcsLogMetadata?.ToLowerInvariant() }
        };

        if (containerSpec.FirelensConfiguration.CustomConfigSource is { Type: not FireLensCustomConfigSourceType.None } src)
        {
            options.Add("config-file-type", src.Type.ToString().ToLowerInvariant());
            options.Add("config-file-value", src.FilePath);
        }

        return new Cfn.FirelensConfiguration
        {
            Type = containerSpec.FirelensConfiguration.FirelensType?.ToString().ToLowerInvariant(),
            Options = options
        };
    }

    public static Cfn.Secret[] ParseSecrets(this ContainerSpec containerSpec)
    {
        var secrets = containerSpec.EnvironmentVariables
                                   .Where(tkp => tkp.Type == KeyValueType.Secret)
                                   .GroupBy(kvp => kvp.Key)
                                   .Select(g => new Cfn.Secret { Name = g.Key, ValueFrom = g.Last().Value })
                                   .ToArray();
        return secrets.Length == 0 ? null : secrets;
    }
}