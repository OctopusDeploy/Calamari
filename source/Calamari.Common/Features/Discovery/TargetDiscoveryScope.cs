using System.Collections.Generic;
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
            var failureReasons = new List<string>();
            if (tags.Role == null)
            {
                failureReasons.Add(
                    $"Missing role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", Roles)}'].");
            }
            else if (!Roles.Contains(tags.Role))
            {
                failureReasons.Add(
                    $"Mismatched role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", Roles)}'], but found '{tags.Role}'.");
            }

            if (tags.Environment == null)
            {
                failureReasons.Add(
                    $"Missing environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{EnvironmentName}'.");
            }
            else if (tags.Environment != EnvironmentName)
            {
                failureReasons.Add(
                    $"Mismatched environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{EnvironmentName}', but found '{tags.Environment}'.");
            }

            if (tags.Project != null && tags.Project != this.ProjectName)
            {
                failureReasons.Add(
                    $"Mismatched project tag. Optional '{TargetTags.ProjectTagName}' tag must match '{ProjectName}' if present, but is '{tags.Project}'.");
            }

            if (tags.Space != null && tags.Space != this.SpaceName)
            {
                failureReasons.Add(
                    $"Mismatched space tag. Optional '{TargetTags.SpaceTagName}' tag must match '{SpaceName}' if present, but is '{tags.Space}'.");
            }

            if (tags.Tenant != null && tags.Tenant != this.TenantName)
            {
                failureReasons.Add(
                    $"Mismatched tenant tag. Optional '{TargetTags.TenantTagName}' tag must match '{TenantName}' if present, but is '{tags.Tenant}'.");
            }

            return failureReasons.Any()
                ? TargetMatchResult.Failure(failureReasons)
                : TargetMatchResult.Success(this.Roles.First(r => r == tags.Role));
        }
    }
}
