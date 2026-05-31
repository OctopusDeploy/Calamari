using System;
using System.Linq;

namespace Calamari.Testing.Azure
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
            //"westus2", - 2026-04-23   westus2 is low in capacity now
            "australiaeast"
        };

        public static string GetRandomRegionWithExclusions(params string[] excludedRegions)
        {
            var possibleRegions = regions.Except(excludedRegions).ToArray();

            return possibleRegions[random.Next(0, possibleRegions.Length)];
        }
    }
}