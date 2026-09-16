using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Calamari.Contracts.TargetDiscovery;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Discovery;

public static class TargetDiscoveryScopeExtensionMethods
{
    public static TargetMatchResult Match(this TargetDiscoveryScope scope, TargetTags tags)
        {
            var failureReasons = new List<string>();
            if (tags.Role == null)
            {
                failureReasons.Add(
                    $"Missing role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", scope.Roles)}'].");
            }
            else if (!scope.Roles.Any(r => r.Equals(tags.Role, StringComparison.OrdinalIgnoreCase)))
            {
                failureReasons.Add(
                    $"Mismatched role tag. Match requires '{TargetTags.RoleTagName}' tag with value from ['{string.Join("', '", scope.Roles)}'], but found '{tags.Role}'.");
            }

            if (tags.Environment == null)
            {
                failureReasons.Add(
                    $"Missing environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{scope.EnvironmentName}'.");
            }
            else if (!tags.Environment.Equals(scope.EnvironmentName, StringComparison.OrdinalIgnoreCase))
            {
                failureReasons.Add(
                    $"Mismatched environment tag. Match requires '{TargetTags.EnvironmentTagName}' tag with value '{scope.EnvironmentName}', but found '{tags.Environment}'.");
            }

            if (tags.Project != null && !tags.Project.Equals(scope.ProjectName, StringComparison.OrdinalIgnoreCase))
            {
                failureReasons.Add(
                    $"Mismatched project tag. Optional '{TargetTags.ProjectTagName}' tag must match '{scope.ProjectName}' if present, but is '{tags.Project}'.");
            }

            if (tags.Space != null && !tags.Space.Equals(scope.SpaceName, StringComparison.OrdinalIgnoreCase))
            {
                failureReasons.Add(
                    $"Mismatched space tag. Optional '{TargetTags.SpaceTagName}' tag must match '{scope.SpaceName}' if present, but is '{tags.Space}'.");
            }

            if (tags.Tenant != null && !tags.Tenant.Equals(scope.TenantName, StringComparison.OrdinalIgnoreCase))
            {
                failureReasons.Add(
                    $"Mismatched tenant tag. Optional '{TargetTags.TenantTagName}' tag must match '{scope.TenantName}' if present, but is '{tags.Tenant}'.");
            }
            
            return failureReasons.Any()
                ? TargetMatchResult.Failure(failureReasons)
                : TargetMatchResult.Success(scope.Roles.First(r => r.Equals(tags.Role, StringComparison.OrdinalIgnoreCase)), tags.TenantedDeploymentMode);
        }
}