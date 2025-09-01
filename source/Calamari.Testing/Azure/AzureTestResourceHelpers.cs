using System;
using System.Collections.Generic;

namespace Calamari.Testing.Azure;

public static class AzureTestResourceHelpers
{
    const string ValidNameChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    static readonly Random Random = new();

    public static string GetResourceGroupName()
    {
        //surely the changes of hitting 8 random chars on the same day at the same time are unique
        return RandomName($"calamari-e2e-{DateTime.UtcNow:yyyyMMdd}-", 8);
    }

    public static string RandomName(string? prefix = null, int length = 32)
    {
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = ValidNameChars[Random.Next(ValidNameChars.Length)];
        }

        return $"{prefix}{new string(result)}";
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