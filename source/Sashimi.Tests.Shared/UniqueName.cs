using System;

namespace Sashimi.Tests.Shared
{
    public class UniqueName
    {
        public static string Generate()
        {
            return Guid.NewGuid().ToString("N").ToLowerInvariant();
        }

        public static string Short()
        {
            return Generate().Substring(0, 10);
        }
    }
}