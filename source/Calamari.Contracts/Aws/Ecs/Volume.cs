namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record Volume
{
    public VolumeType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? FileSystemId { get; init; }
    public string? AccessPointId { get; init; }
    public string? RootDirectory { get; init; }
    public string EncryptionInTransit { get; init; } = string.Empty;
    public string EfsIamAuthorization { get; init; } = string.Empty;
}

public enum VolumeType
{
    Bind,
    Efs
}
