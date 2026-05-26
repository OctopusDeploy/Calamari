namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerSpec
{
    public string ContainerName { get; init; } = string.Empty;
    public ContainerImageReference ContainerImageReference { get; init; } = new();
    public RepositoryAuthentication RepositoryAuthentication { get; init; } = new();
    public string? MemoryLimitSoft { get; init; }
    public string? MemoryLimitHard { get; init; }
    public List<ContainerPortMapping> ContainerPortMappings { get; init; } = [];
    public string? Cpus { get; init; }
    public string? Gpus { get; init; }
    public string Essential { get; init; } = string.Empty;
    public string? EntryPoint { get; init; }
    public string? Command { get; init; }
    public string? WorkingDirectory { get; init; }
    public List<string> EnvironmentFiles { get; init; } = [];
    public List<TypedKeyValuePair> EnvironmentVariables { get; init; } = [];
    public ContainerNetworkSettings NetworkSettings { get; init; } = new();
    public ContainerStorage ContainerStorage { get; init; } = new();
    public ContainerLogging ContainerLogging { get; init; } = new();
    public ContainerFireLensConfiguration FirelensConfiguration { get; init; } = new();
    public List<KeyValuePair<string, string>> DockerLabels { get; init; } = [];
    public string? User { get; init; }
    public HealthCheck HealthCheck { get; init; } = new();
    public List<ContainerDependency> Dependencies { get; init; } = [];
    public string? StartTimeout { get; init; }
    public string? StopTimeout { get; init; }
    public List<Ulimit> Ulimits { get; init; } = [];
}

public record ContainerImageReference
{
    public string ReferenceId { get; init; } = string.Empty;
    public string ImageName { get; init; } = string.Empty;
    public string FeedId { get; init; } = string.Empty;
}

public record RepositoryAuthentication
{
    public RepositoryAuthenticationType Type { get; init; }
    public string? SecretName { get; init; }
}

public enum RepositoryAuthenticationType
{
    Default,
    SecretsManager
}

public record ContainerPortMapping
{
    public string ContainerPort { get; init; } = string.Empty;
    public PortProtocol Protocol { get; init; }
}

public enum PortProtocol
{
    Tcp,
    Udp
}

public record ContainerDependency
{
    public string ContainerName { get; init; } = string.Empty;
    public ContainerDependencyCondition Condition { get; init; }
}

public enum ContainerDependencyCondition
{
    Start,
    Complete,
    Success,
    Healthy
}

public record Ulimit
{
    public string LimitName { get; init; } = string.Empty;
    public string HardLimit { get; init; } = string.Empty;
    public string SoftLimit { get; init; } = string.Empty;
}
