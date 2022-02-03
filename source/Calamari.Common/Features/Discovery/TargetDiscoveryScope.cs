namespace Calamari.Common.Features.Discovery
{
    public class TargetDiscoveryScope
    {
        public TargetDiscoveryScope(
            string spaceId,
            string environmentId,
            string projectId,
            string? tenantId,
            string[] roles,
            string? workerPoolId)
        {
            SpaceId = spaceId;
            EnvironmentId = environmentId;
            ProjectId = projectId;
            TenantId = tenantId;
            Roles = roles;
            WorkerPoolId = workerPoolId;
        }

        // todo: Need to work out whether this should be id, name or maybe slug
        public string SpaceId { get; private set; }
        public string EnvironmentId { get; private set; }
        public string ProjectId { get; private set; }
        public string? TenantId { get; private set; }
        public string[] Roles { get; private set; }
        public string? WorkerPoolId { get; private set; }
    }
}
