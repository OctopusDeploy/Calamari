using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public static class MigratedCalamariFlavours
    {
        public static List<string> Flavours => FullFrameworkWindowsOnlyFlavours.Concat(NetCoreEnabledFlavours).Concat(WindowsOnlyNetCoreEnabledFlavours).ToList();

        public static readonly List<string> FullFrameworkWindowsOnlyFlavours = new()
        {
            "Calamari.AzureWebApp",
            "Calamari.",
        };

        public static readonly List<string> NetCoreEnabledFlavours = new()
        {
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureScripting",
            "Calamari.Terraform"
        };

        public static readonly List<string> WindowsOnlyNetCoreEnabledFlavours = new()
        {
            "Calamari.AzureServiceFabric",
        };
    }
}