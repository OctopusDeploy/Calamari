using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.ConsolidateCalamariPackages
{
    public class MigratedCalamariFlavours
    {
        public static List<string> Flavours => FullFrameworkOnlyFlavours.Concat(CrossPlatformFlavours).ToList();

        public static List<string> FullFrameworkOnlyFlavours = new()
        {
            "Calamari.AzureServiceFabric",
            "Calamari.AzureCloudService",
        };

        public static List<string> CrossPlatformFlavours = new()
        {
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.AzureWebApp",
            "Calamari.Terraform"
        };
    }
}