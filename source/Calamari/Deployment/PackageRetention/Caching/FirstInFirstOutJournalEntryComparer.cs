using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FirstInFirstOutJournalEntryComparer : IComparer<JournalEntry>
    {
        public int Compare(JournalEntry x, JournalEntry y)
        {
            return GetCacheAgeAtFirstPackageUse(x).CompareTo(GetCacheAgeAtFirstPackageUse(y));
        }

        static int GetCacheAgeAtFirstPackageUse(JournalEntry entry)
        {
            return entry?.GetUsageDetails()?.Min(m => m.CacheAgeAtUsage.Value)
                   ?? int.MaxValue;
        }
    }
}