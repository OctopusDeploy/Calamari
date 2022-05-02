using System;
using System.Linq;
using Calamari.Common.Features.Discovery;
using Microsoft.Azure.Management.AppService.Fluent;

namespace Calamari.CloudAccounts.Azure
{
    public static class AzureWebAppHelper
    {
        public static TargetSite GetAzureTargetSite(string siteAndMaybeSlotName, string slotName, string resourceGroupName)
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

        public static TargetTags GetOctopusTags(IWebAppBasic webApp)
        {
            var caseInsensitiveTagDictionary = webApp.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.EnvironmentTagName, out var environment);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.RoleTagName, out var role);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.ProjectTagName, out var project);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.SpaceTagName, out var space);
            caseInsensitiveTagDictionary.TryGetValue(TargetTags.TenantTagName, out var tenant);
            return new TargetTags(
                environment: environment,
                role: role,
                project: project,
                space: space,
                tenant: tenant);
        }
    }
}