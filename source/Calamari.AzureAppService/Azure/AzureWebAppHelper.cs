using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Discovery;

#nullable enable
namespace Calamari.AzureAppService.Azure
{
    static class AzureWebAppHelper
    {
        public static TargetTags GetOctopusTags(IReadOnlyDictionary<string, string> tags)
        {
            var caseInsensitiveTagDictionary = tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.EnvironmentTagName, out string? environment);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.RoleTagName, out string? role);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.ProjectTagName, out string? project);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.SpaceTagName, out string? space);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.TenantTagName, out string? tenant);
            return new TargetTags(
                environment: environment,
                role: role,
                project: project,
                space: space,
                tenant: tenant);
        }
    }
}
#nullable restore