using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public static class CalamariPackages
    {
        public static List<string> Flavours => CrossPlatformFlavours.ToList();

        static readonly List<string> CrossPlatformFlavours = new()
        {
            "Calamari.AzureServiceFabric",
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.AzureWebApp",
            "Calamari.Terraform"
        };
    }
}
