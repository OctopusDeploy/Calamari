namespace Calamari.Aws.Integration.Ecs;

public record EcsContainerUpdate(
    string ContainerName,
    string Image,
    EnvAction<EnvVarItem> EnvironmentVariables,
    EnvAction<string> EnvironmentFiles);
