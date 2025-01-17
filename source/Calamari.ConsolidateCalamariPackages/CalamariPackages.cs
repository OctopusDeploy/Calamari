using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class CalamariPackages
    {
        public static List<string> Flavours => CrossPlatformPackages;

        static List<string> CrossPlatformPackages = new()
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