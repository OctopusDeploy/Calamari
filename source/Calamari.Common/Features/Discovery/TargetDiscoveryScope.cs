using System.Linq;

namespace Calamari.Common.Features.Discovery
{
    public class TargetDiscoveryScope
    {
        public TargetDiscoveryScope(
            string spaceName,
            string environmentName,
            string projectName,
            string? tenantName,
            string[] roles,
            string? workerPoolId)
        {
            SpaceName = spaceName;
            EnvironmentName = environmentName;
            ProjectName = projectName;
            TenantName = tenantName;
            Roles = roles;
            WorkerPoolId = workerPoolId;
        }

        public string SpaceName { get; private set; }
        public string EnvironmentName { get; private set; }
        public string ProjectName { get; private set; }
        public string? TenantName { get; private set; }
        public string[] Roles { get; private set; }
        public string? WorkerPoolId { get; private set; }

        public TargetMatchResult Match(TargetTags tags)
        {
            if (tags.Role == null)
            {
                return TargetMatchResult.Failure(
                    $"Missing role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", Roles)}'].");
            }

            if (!Roles.Contains(tags.Role))
            {
                return TargetMatchResult.Failure(
                    $"Mismatched role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", Roles)}'], but found '{tags.Role}'.");
            }

            if (tags.Environment == null)
            {
                return TargetMatchResult.Failure(
                    $"Missing environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{EnvironmentName}'.");
            }

            if (tags.Environment != EnvironmentName)
            {
                return TargetMatchResult.Failure(
                    $"Mismatched environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{EnvironmentName}', but found '{tags.Environment}'.");
            }

            if (tags.Project != null && tags.Project != this.ProjectName)
            {
                return TargetMatchResult.Failure(
                    $"Mismatched project tag. Optional '{TargetTags.ProjectTagName}' tag must match '{ProjectName}' if present, but is '{tags.Project}'.");
            }

            if (tags.Space != null && tags.Space != this.SpaceName)
            {
                return TargetMatchResult.Failure(
                    $"Mismatched space tag. Optional '{TargetTags.SpaceTagName}' tag must match '{SpaceName}' if present, but is '{tags.Space}'.");
            }

            if (tags.Tenant != null && tags.Tenant != this.TenantName)
            {
                return TargetMatchResult.Failure(
                    $"Mismatched tenant tag. Optional '{TargetTags.TenantTagName}' tag must match '{TenantName}' if present, but is '{tags.Tenant}'.");
            }

            return TargetMatchResult.Success(this.Roles.First(r => r == tags.Role));
        }
    }
}
