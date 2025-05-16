using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Microsoft.Azure.Management.ContainerService.Fluent.Models;
using TimeSpan = System.TimeSpan;

namespace PackageCacheAnalyzer
{
    public class KeepDaysFilteredJournal
    {
        
        JsonJournalRepository.PackageData packageData;
        int days;

        public KeepDaysFilteredJournal(JsonJournalRepository.PackageData packageData, int days)
        {
            this.packageData = packageData;
            this.days = days;
        }

        public List<JournalEntry> GetEntries(int cacheAge)
        {
            var usageAtAge = packageData.JournalEntries.SelectMany(e => e.GetUsageDetails()).Single(entry => entry.CacheAgeAtUsage.Value == cacheAge);
            return packageData.JournalEntries.Where(entry => entry.GetUsageDetails().GetCacheAgeAtFirstPackageUse().Value <= cacheAge)
                              .Where(entry => entry.GetUsageDetails().Max(e => e.DateTime) > usageAtAge.DateTime.Subtract(TimeSpan.FromDays(days)))
                              .ToList();
        }
    }
}