using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace PackageCacheAnalyzer
{
    public class TakeNFilteredJournal
    {
        JsonJournalRepository.PackageData packageData;
        int take;

        public TakeNFilteredJournal(JsonJournalRepository.PackageData packageData, int take)
        {
            this.packageData = packageData;
            this.take = take;
        }

        public List<JournalEntry> GetEntries(int cacheAge)
        {
            return packageData.JournalEntries.Where(entry => entry.GetUsageDetails().GetCacheAgeAtFirstPackageUse().Value <= cacheAge)
                              .GroupBy(entry => entry.Package.PackageId)
                              .SelectMany(group => group.OrderByDescending(e => e.GetUsageDetails().GetCacheAgeAtFirstPackageUse().Value).Take(take))
                              .ToList();
        }
    }
}