namespace Calamari.Azure.Util
{
    public static class AzureWebAppHelper
    {
        public static void ConvertLegacyAzureWebAppSlotNames(ref string siteName)
        {
            if (!siteName.EndsWith(")")) return;

            // This is an older site that was established with the legacy Azure site and slot names, convert to new "/" style.
            var finalSiteName = siteName.Replace("(", "/").Replace(")", "");
            Log.Verbose($"Converting legacy siteName {siteName} to use the new style expected by Azure SDK {finalSiteName}");
            siteName = finalSiteName;
        }
    }
}
