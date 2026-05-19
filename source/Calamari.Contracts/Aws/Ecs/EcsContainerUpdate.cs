namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record EcsContainerUpdate(
    string ContainerName,
    string? Image,
    EnvAction<EnvVarItem>? EnvironmentVariables,
    EnvAction<string>? EnvironmentFiles);

public record EnvAction<T>(EnvActionMode Action, IReadOnlyList<T> Items);

public record EnvVarItem(EnvVarType Type, string Key, string Value);

public enum EnvActionMode
{
    Replace,
    Merge
}

public enum EnvVarType
{
    Plain,
    Secret
}
