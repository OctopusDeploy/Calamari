namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record LoadBalancerMapping
{
    public string ContainerName { get; init; } = string.Empty;
    public string ContainerPort { get; init; } = string.Empty;
    public string TargetGroupArn { get; init; } = string.Empty;
}
