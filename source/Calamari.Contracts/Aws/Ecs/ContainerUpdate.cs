namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerUpdate
{
    public string ContainerName { get; init; } = string.Empty;
    public string? PackageReference { get; init; }
    public EnvAction<TypedKeyValuePair>? EnvironmentVariables { get; init; }
    public EnvAction<string>? EnvironmentFiles { get; init; }
}

public record EnvAction<T>
{
    public EnvActionMode Action { get; init; }
    public List<T> Items { get; init; } = [];
}

public enum EnvActionMode
{
    Replace,
    Append
}
