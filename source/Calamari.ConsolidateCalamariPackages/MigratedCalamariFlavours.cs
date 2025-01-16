using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class MigratedCalamariFlavours
    {
        public static List<string> Flavours => FullFrameworkOnlyFlavours.Concat(CrossPlatformFlavours).ToList();

        public static List<string> FullFrameworkOnlyFlavours = new()
        {
            "Calamari.AzureWebApp",
            "Calamari.",
        };

        public static List<string> CrossPlatformFlavours = new()
        {
            "Calamari.AzureServiceFabric",
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.Terraform"
        };
    }
}