namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record WaitOption(WaitMode Mode, string? Timeout);

public enum WaitMode
{
    DontWait,
    WaitUntilCompleted,
    WaitWithTimeout
}
