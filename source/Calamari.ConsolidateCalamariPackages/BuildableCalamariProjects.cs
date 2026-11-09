using System;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public static class BuildableCalamariProjects
    {
        public static string[] GetAllCalamariProjects() => [..NonWindows,..Windows];

        // We don't build docker images for windows projects
        public static string[] GetCalamariProjectsForDockerImages => NonWindows;
        
        public static string[] GetCalamariProjectsToBuild(bool isWindows)
        {
            return isWindows
                ? [..NonWindows,..Windows]
                : NonWindows;
        }
        
        static readonly string[] NonWindows =
        [
            "Calamari",
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.Terraform"
        ];

        static string[] Windows => ["Calamari.AzureWebApp", "Calamari.AzureServiceFabric"];
    }
}