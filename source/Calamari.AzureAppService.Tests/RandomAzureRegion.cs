using System;
using System.Linq;
using JetBrains.Annotations;

namespace Calamari.AzureAppService.Tests
{
    public static class RandomAzureRegion
    {
        static Random random = new Random();

        static string[] regions = new[]
        {
            "southeastasia",
            "centralus",
            "eastus",
            "eastus2",
            "westus",
            "westus2",
            "westus3",
            "australiaeast"
        };

        public static string GetRandomRegion(params string[] excludedRegions)
        {
            var possibleRegions = regions.Except(excludedRegions).ToArray();

            return possibleRegions[random.Next(0, regions.Length)];
        }
    }
}