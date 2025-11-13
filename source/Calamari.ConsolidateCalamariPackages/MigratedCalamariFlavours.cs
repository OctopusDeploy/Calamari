using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public static class MigratedCalamariFlavours
    {
        public static List<string> Flavours => FullFrameworkOnlyFlavours.Concat(CrossPlatformFlavours).ToList();

        public static List<string> FullFrameworkOnlyFlavours = new()
        {
            "Calamari.AzureWebApp",
            "Calamari.AzureServiceFabric",
            "Calamari.",
        };

        public static List<string> CrossPlatformFlavours = new()
        {
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.Terraform"
        };
    }
}