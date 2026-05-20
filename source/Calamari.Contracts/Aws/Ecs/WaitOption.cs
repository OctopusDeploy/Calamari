namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record WaitOption
{
    public WaitType Type { get; init; }
    public string? Timeout { get; init; }
}

public enum WaitType
{
    DontWait,
    WaitUntilCompleted,
    WaitWithTimeout
}
