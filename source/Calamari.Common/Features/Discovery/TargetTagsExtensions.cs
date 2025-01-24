using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Common.Features.Discovery
{
    public static class TargetTagsExtensions
    {
        public static TargetTags ToTargetTags(this IEnumerable<KeyValuePair<string, string>> tags)
        {
            var caseInsensitiveTagDictionary = tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.EnvironmentTagName, out var environment);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.RoleTagName, out var role);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.ProjectTagName, out var project);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.SpaceTagName, out var space);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.TenantTagName, out var tenant);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.TenantedDeploymentModeTagName, out var tenantedDeploymentMode);
            return new TargetTags(
                environment: environment,
                role: role,
                project: project,
                space: space,
                tenant: tenant,
                tenantedDeploymentMode: tenantedDeploymentMode);
        }
    }
}