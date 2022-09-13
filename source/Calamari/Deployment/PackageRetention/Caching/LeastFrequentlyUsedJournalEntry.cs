using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class LeastFrequentlyUsedJournalEntry
    {
        public JournalEntry Entry { get; }
        public CacheAge Age { get; }
        public int HitCount { get; }
        public int NewerVersionCount { get; }

        public LeastFrequentlyUsedJournalEntry(JournalEntry entry, CacheAge age, int hitCount, int newerVersionCount)
        {
            Entry = entry;
            Age = age;
            HitCount = hitCount;
            NewerVersionCount = newerVersionCount;
        }
    }
}