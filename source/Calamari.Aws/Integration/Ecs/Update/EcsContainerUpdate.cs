namespace Calamari.Aws.Integration.Ecs.Update;

public record EcsContainerUpdate(
    string ContainerName,
    string Image,
    EnvVarAction EnvironmentVariables,
    EnvFileAction EnvironmentFiles)
{
    public EcsContainerUpdate() : this(string.Empty, null, EnvVarAction.None, EnvFileAction.None) { }
}
