namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record HealthCheck
{
    public List<string> Command { get; init; } = [];
    public string? Interval { get; init; }
    public string? Retries { get; init; }
    public string? StartPeriod { get; init; }
    public string? Timeout { get; init; }
}
