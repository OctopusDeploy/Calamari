using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace PackageCacheAnalyzer
{
    public class FilteredJournal
    {
        JsonJournalRepository.PackageData packageData;

        public FilteredJournal(JsonJournalRepository.PackageData packageData)
        {
            this.packageData = packageData;
        }

        public List<JournalEntry> GetEntries(int cacheAge)
        {
            return packageData.JournalEntries.Where(entry => entry.GetUsageDetails().GetCacheAgeAtFirstPackageUse().Value <= cacheAge).ToList();
        }
    }
}