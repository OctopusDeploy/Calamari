using System;

namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record WaitOption
{
    public WaitType Type { get; init; }

    public string? TimeoutMinutes { get; init; }

    public TimeSpan? GetTimeoutSpan() =>
        int.TryParse(TimeoutMinutes, out var minutes) && minutes >= 0
            ? TimeSpan.FromMinutes(minutes)
            : null;
}

public enum WaitType
{
    DontWait,
    WaitUntilCompleted,
    WaitWithTimeout
}
