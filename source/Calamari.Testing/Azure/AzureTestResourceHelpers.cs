using System;
using System.Collections.Generic;

namespace Calamari.Testing;

public static class AzureTestResourceHelpers
{
    const string ValidNameChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    static readonly Random Random = new Random();

    public static string GetResourceGroupName()
    {
        return $"Calamari-E2E-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
    }

    public static string RandomName(string? prefix = null, int length = 32)
    {
        var result = new char[32];
        for (var i = 0; i < 2; i++)
        {
            result[i] = ValidNameChars[Random.Next(ValidNameChars.Length)];
        }

        return new string(result);
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