using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public static class ListPackageInfoExtensionMethods
    {
        //Note that the age max/min seem backwards, but that's because the larger value is more recent (we record the age of the *cache* at the time of use).
        public static (int Min, int Max) GetCacheAgeRange(this List<LeastFrequentlyUsedJournalEntry> details, CacheAge currentCacheAge) => (currentCacheAge.Value - details.Max(d => d.Age.Value), currentCacheAge.Value - details.Min(d => d.Age.Value));
        public static (int Min, int Max) GetNewVersionCountRange(this List<LeastFrequentlyUsedJournalEntry> details) => (details.Min(d => d.NewerVersionCount), details.Max(d => d.NewerVersionCount));
        public static (int Min, int Max) GetHitCountRange(this List<LeastFrequentlyUsedJournalEntry> details) => (details.Min(d => d.HitCount), details.Max(d => d.HitCount));
    }
}