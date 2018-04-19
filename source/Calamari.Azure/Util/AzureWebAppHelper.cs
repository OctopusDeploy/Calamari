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
    }
}