using System;
using System.Collections.Generic;

namespace Calamari.ConsolidateCalamariPackages
{
    public class CalamariPackages
    {
        public static List<string> Flavours => CrossPlatformPackages;

        static readonly List<string> CrossPlatformPackages = new()
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