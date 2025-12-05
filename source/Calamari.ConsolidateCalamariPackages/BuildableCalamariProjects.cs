using System;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public static class BuildableCalamariProjects
    {
        public static string[] GetCalamariProjectsToBuild(bool isWindows)
        {
            return isWindows
                ? Windows
                : NonWindows;
        }
        
        static readonly string[] NonWindows =
        [
            "Calamari",
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.AzureWebApp",
            "Calamari.Terraform"
        ];
        
        static string[] Windows => [..NonWindows, "Calamari.AzureServiceFabric"];
    }
}