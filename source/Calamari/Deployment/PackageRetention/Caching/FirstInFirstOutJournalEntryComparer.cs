using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FirstInFirstOutJournalEntryComparer : IComparer<JournalEntry>
    {
        public int Compare(JournalEntry x, JournalEntry y)
        {
            return GetCacheAgeAtFirstPackageUse(x).CompareTo(GetCacheAgeAtFirstPackageUse(y));
        }

        static CacheAge GetCacheAgeAtFirstPackageUse(JournalEntry entry)
        {
            return entry.GetUsageDetails().GetCacheAgeAtFirstPackageUse();
        }
    }
}