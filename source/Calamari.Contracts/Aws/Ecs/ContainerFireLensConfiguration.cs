namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerFireLensConfiguration
{
    public FireLensConfigurationType Type { get; init; }
    public FireLensType? FirelensType { get; init; }
    public string EnableEcsLogMetadata { get; init; } = string.Empty;
    public FireLensCustomConfigSource? CustomConfigSource { get; init; }
}

public enum FireLensConfigurationType
{
    Disabled,
    Enabled
}

public enum FireLensType
{
    Fluentd,
    Fluentbit
}

public record FireLensCustomConfigSource
{
    public FireLensCustomConfigSourceType Type { get; init; }
    public string? FilePath { get; init; }
}

public enum FireLensCustomConfigSourceType
{
    None,
    File,
    S3
}
