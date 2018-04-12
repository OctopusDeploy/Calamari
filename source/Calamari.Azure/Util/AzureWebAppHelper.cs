namespace Calamari.Azure.Util
{
    public static class AzureWebAppHelper
    {
        public static string ConvertLegacyAzureWebAppSlotNames(string siteName)
        {
            if (!siteName.EndsWith(")")) return siteName;

            // This is an older site that was established with the legacy Azure site and slot names, convert to new "/" style.
            var finalSiteName = siteName.Replace("(", "/").Replace(")", "");
            Log.Verbose($"Converting legacy siteName {siteName} to use the new style expected by Azure SDK {finalSiteName}");
            return finalSiteName;
        }

        /// <summary>
        /// The current way of processing slots from Azure's SDK (they use slashes now).
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="deploymentSlot"></param>
        /// <returns></returns>
        public static string GetSiteAndSlotName(string siteName, string deploymentSlot)
        {
            var siteAndSlot = siteName;
            if (siteName.Contains("/"))
            {
                Log.Verbose($"Using the deployment slot found on the site name {siteName}.");
            }
            else if (!string.IsNullOrWhiteSpace(deploymentSlot))
            {
                Log.Verbose($"Using the deployment slot as defined on the step ({deploymentSlot}).");
                siteAndSlot = $"{siteName}/{deploymentSlot}";
            }
            return siteAndSlot;
        }

        /// <summary>
        /// Older Azure SDK used to return slots in brackets, new Azure returns it with a slash, dealing with legacy here.
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="deploymentSlot"></param>
        /// <returns></returns>
        public static string GetLegacySiteAndSlotName(string siteName, string deploymentSlot)
        {
            var siteAndSlot = siteName;
            if (siteName.Contains("/") || siteName.EndsWith(")"))
            {
                Log.Verbose($"Using the deployment slot found on the site name {siteName}.");
            }
            else if (!string.IsNullOrWhiteSpace(deploymentSlot))
            {
                Log.Verbose($"Using the deployment slot as defined on the step ({deploymentSlot}).");
                siteAndSlot = $"{siteName}({deploymentSlot})";
            }
            return siteAndSlot;
        }
    }
}