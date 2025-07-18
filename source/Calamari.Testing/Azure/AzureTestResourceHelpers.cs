using System;
using System.Collections.Generic;

namespace Calamari.Testing;

public static class AzureTestResourceHelpers
{
    public static string GetResourceGroupName()
    {
        return $"Calamari-E2E-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
    }

    public static class ResourceGroupTags
    {
        public const string LifetimeInDaysKey = "LifetimeInDays";
        public const string LifetimeInDaysValue = "1";

        public const string SourceKey = "source";
        public const string SourceValue = "calamari-e2e-tests";

        public static Dictionary<string, string> ToDictionary()
        {
            
            return new Dictionary<string, string>
            {
                [LifetimeInDaysKey] = LifetimeInDaysValue,
                [SourceKey] = SourceValue
            };
        }
    }
}