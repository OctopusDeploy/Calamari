using System.Collections.Generic;

namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record ContainerDefinition
{
    public string Name { get; init; }
    public string Image { get; init; }
    public bool? Essential { get; init; }
    public bool? DisableNetworking { get; init; }
    public string WorkingDirectory { get; init; }
    public double? Memory { get; init; }
    public double? MemoryReservation { get; init; }
    public double? Cpu { get; init; }
    public string User { get; init; }
    public double? StartTimeout { get; init; }
    public double? StopTimeout { get; init; }
    public string[] DnsServers { get; init; }
    public string[] DnsSearchDomains { get; init; }
    public bool? ReadonlyRootFilesystem { get; init; }
    public string[] Command { get; init; }
    public string[] EntryPoint { get; init; }
    public ResourceRequirement[] ResourceRequirements { get; init; }
    public Dictionary<string, string> DockerLabels { get; init; }
    public PortMapping[] PortMappings { get; init; }
    public HealthCheck HealthCheck { get; init; }
    public ExtraHost[] ExtraHosts { get; init; }
    public RepositoryCredentials RepositoryCredentials { get; init; }
    public Ulimit[] Ulimits { get; init; }
    public MountPoint[] MountPoints { get; init; }
    public ContainerDependency[] DependsOn { get; init; }
    public VolumeFrom[] VolumesFrom { get; init; }
    public LogConfiguration LogConfiguration { get; init; }
    public EnvironmentFile[] EnvironmentFiles { get; init; }
    public FirelensConfiguration FirelensConfiguration { get; init; }
    public EnvironmentEntry[] Environment { get; init; }
    public Secret[] Secrets { get; init; }
}

public sealed record ResourceRequirement
{
    public string Type { get; init; }
    public string Value { get; init; }
}

public sealed record PortMapping
{
    public double? ContainerPort { get; init; }
    public double? HostPort { get; init; }
    public string Protocol { get; init; }
}

public sealed record HealthCheck
{
    public string[] Command { get; init; }
    public double? Interval { get; init; }
    public double? Retries { get; init; }
    public double? StartPeriod { get; init; }
    public double? Timeout { get; init; }
}

public sealed record ExtraHost
{
    public string Hostname { get; init; }
    public string IpAddress { get; init; }
}

public sealed record RepositoryCredentials
{
    public string CredentialsParameter { get; init; }
}

public sealed record Ulimit
{
    public string Name { get; init; }
    public double HardLimit { get; init; }
    public double SoftLimit { get; init; }
}

public sealed record MountPoint
{
    public string SourceVolume { get; init; }
    public string ContainerPath { get; init; }
    public bool? ReadOnly { get; init; }
}

public sealed record ContainerDependency
{
    public string ContainerName { get; init; }
    public string Condition { get; init; }
}

public sealed record VolumeFrom
{
    public string SourceContainer { get; init; }
    public bool? ReadOnly { get; init; }
}

// LogConfiguration.Options values can be either a literal string or a Ref intrinsic
// (e.g. awslogs-group → Ref(LogGroupName), awslogs-region → Ref(AWS::Region)).
public sealed record LogConfiguration
{
    public string LogDriver { get; init; }
    public Dictionary<string, Value<string>> Options { get; init; }
    public Secret[] SecretOptions { get; init; }
}

public sealed record FirelensConfiguration
{
    public string Type { get; init; }
    public Dictionary<string, string> Options { get; init; }
}

// Renamed from "KeyValuePair" to avoid clashing with System.Collections.Generic.KeyValuePair.
public sealed record EnvironmentEntry
{
    public string Name { get; init; }
    public string Value { get; init; }
}

public sealed record Secret
{
    public string Name { get; init; }
    public string ValueFrom { get; init; }
}

public sealed record EnvironmentFile
{
    public string Type { get; init; }
    public string Value { get; init; }
}
