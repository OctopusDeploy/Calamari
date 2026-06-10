namespace Octopus.Calamari.Contracts.TargetDiscovery;

public class TargetDiscoveryScope
{
    public TargetDiscoveryScope(
        string spaceName,
        string environmentName,
        string projectName,
        string? tenantName,
        string[] roles,
        string? workerPoolId,
        FeedImage? healthCheckContainer)
    {
        SpaceName = spaceName;
        EnvironmentName = environmentName;
        ProjectName = projectName;
        TenantName = tenantName;
        Roles = roles;
        WorkerPoolId = workerPoolId;
        HealthCheckContainer = healthCheckContainer;
    }

    public string SpaceName { get; private set; }
    public string EnvironmentName { get; private set; }
    public string ProjectName { get; private set; }
    public string? TenantName { get; private set; }
    public string[] Roles { get; private set; }
    public string? WorkerPoolId { get; private set; }
    public FeedImage? HealthCheckContainer { get; private set; }
}

public record FeedImage(string ImageNameAndTag, string FeedIdOrName);