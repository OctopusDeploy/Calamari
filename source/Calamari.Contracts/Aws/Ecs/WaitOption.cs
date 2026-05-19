namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record WaitOption(WaitMode Mode, TimeSpan? Timeout);

public enum WaitMode
{
    DontWait,
    WaitUntilCompleted,
    WaitWithTimeout
}
