using System;
using System.Linq;

namespace Calamari.Testing
{
    public static class RandomAzureRegion
    {
        static readonly Random Random = new();

        static readonly string[] Regions = {
            "southeastasia",
            "centralus",
            "eastus",
            "eastus2",
            "westus",
            "westus2",
            "australiaeast",
            "australiasoutheast"
        };

        public static string GetRandomRegionWithExclusions(params string[] excludedRegions)
        {
            var possibleRegions = Regions.Except(excludedRegions).ToArray();

            return possibleRegions[Random.Next(0, possibleRegions.Length)];
        }
    }
}