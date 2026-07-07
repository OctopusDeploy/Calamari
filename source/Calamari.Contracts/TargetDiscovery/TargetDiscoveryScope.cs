namespace Octopus.Calamari.Contracts.TargetDiscovery;

public class TargetDiscoveryScope(
    string spaceName,
    string environmentName,
    string projectName,
    string? tenantName,
    string[] roles,
    string? workerPoolId,
    FeedImage? healthCheckContainer)
{
    public string SpaceName { get; private set; } = spaceName;
    public string EnvironmentName { get; private set; } = environmentName;
    public string ProjectName { get; private set; } = projectName;
    public string? TenantName { get; private set; } = tenantName;
    public string[] Roles { get; private set; } = roles;
    public string? WorkerPoolId { get; private set; } = workerPoolId;
    public FeedImage? HealthCheckContainer { get; private set; } = healthCheckContainer;
}

public record FeedImage(string ImageNameAndTag, string FeedIdOrName);