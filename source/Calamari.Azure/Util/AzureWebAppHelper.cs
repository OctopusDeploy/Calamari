using System;

namespace Calamari.Azure.Util
{
    public static class AzureWebAppHelper
    {
        public static string ConvertLegacyAzureWebAppSlotNames(string siteName)
        {
            if (!siteName.EndsWith(")")) return siteName;

            // This is an older site that was established with the legacy Azure site and slot names, convert to new "/" style.
            var finalSiteName = siteName.Trim().Replace("(", "/").Replace(")", "");
            Log.Verbose($"Converting legacy siteName {siteName} to use the new style expected by Azure SDK {finalSiteName}");
            return finalSiteName;
        }

        public static string GetSiteNameFromSiteAndSlotName(string siteAndSlotName)
        {
            var currentSiteAndSlotName = ConvertLegacyAzureWebAppSlotNames(siteAndSlotName);
            var slashIndex = currentSiteAndSlotName.IndexOf("/", StringComparison.Ordinal);
            return slashIndex == -1 ? currentSiteAndSlotName : currentSiteAndSlotName.Substring(0, slashIndex);
        }
    }
}