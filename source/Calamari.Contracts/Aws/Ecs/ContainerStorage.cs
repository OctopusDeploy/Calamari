namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerStorage
{
    public string ReadOnlyRootFileSystem { get; init; } = string.Empty;
    public List<ContainerMountPoint> MountPoints { get; init; } = [];
    public List<ContainerVolumeFrom> VolumeFrom { get; init; } = [];
}

public record ContainerMountPoint
{
    public string SourceVolume { get; init; } = string.Empty;
    public string ContainerPath { get; init; } = string.Empty;
    public string Readonly { get; init; } = string.Empty;
}

public record ContainerVolumeFrom
{
    public string SourceContainer { get; init; } = string.Empty;
    public string Readonly { get; init; } = string.Empty;
}
