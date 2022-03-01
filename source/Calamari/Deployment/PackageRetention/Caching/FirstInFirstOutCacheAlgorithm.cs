using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FirstInFirstOutCacheAlgorithm : IOrderJournalEntries
    {
        public IEnumerable<JournalEntry> Order(IEnumerable<JournalEntry> journalEntries)
        {
            return journalEntries
                   .Where(e => !e.HasLock())
                   .OrderBy(GetCacheAgeAtFirstPackageUse);

            int GetCacheAgeAtFirstPackageUse(JournalEntry entry)
            {
                return entry?.GetUsageDetails()?.Min(m => m.CacheAgeAtUsage.Value)
                       ?? int.MaxValue;
            }
        }
    }
}