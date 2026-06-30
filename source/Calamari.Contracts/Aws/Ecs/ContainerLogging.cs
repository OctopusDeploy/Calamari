namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerLogging
{
    public ContainerLoggingType Type { get; init; }
    public LogDriver? LogDriver { get; init; }
    public List<TypedKeyValuePair> LogOptions { get; init; } = [];
}

public enum ContainerLoggingType
{
    Auto,
    Manual
}

public enum LogDriver
{
    Default,
    None,
    AwsFirelens,
    AwsLogs,
    Splunk
}
