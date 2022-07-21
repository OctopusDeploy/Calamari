using Calamari.Common.Features.Discovery;
using Microsoft.Azure.Management.AppService.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace Calamari.Azure
{
    static class AzureWebAppHelper
    {
        public static TargetSite GetAzureTargetSite(string siteAndMaybeSlotName, string? slotName, string resourceGroupName)
        {
            var targetSite = new TargetSite {RawSite = siteAndMaybeSlotName};

            if (siteAndMaybeSlotName.Contains("("))
            {
                // legacy site and slot "site(slot)"
                var parenthesesIndex = siteAndMaybeSlotName.IndexOf("(", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, parenthesesIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(parenthesesIndex + 1).Replace(")", string.Empty).Trim();
                return targetSite;
            }

            if (siteAndMaybeSlotName.Contains("/"))
            {
                // "site/slot"
                var slashIndex = siteAndMaybeSlotName.IndexOf("/", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, slashIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(slashIndex + 1).Trim();
                return targetSite;
            }

            targetSite.Site = siteAndMaybeSlotName;
            targetSite.Slot = slotName;
            targetSite.ResourceGroupName = resourceGroupName;
            return targetSite;
        }

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