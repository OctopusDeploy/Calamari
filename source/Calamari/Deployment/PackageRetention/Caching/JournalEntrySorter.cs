using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public static class JournalEntrySorter
    {
        public static IEnumerable<JournalEntry> MostRecentlyUsed(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();

            return entries.OrderByDescending(je => je.GetUsageDetails().Max(u => u.DateTime));
        }
        
        public static IOrderedEnumerable<KeyValuePair<string, IEnumerable<JournalEntry>>> MostRecentlyUsedByVersion(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();
            
            var sortedByVersion = entries
                                  .GroupBy(je => je.Package.PackageId.ToString())
                                  .ToDictionary(k => k.Key, MostRecentlyUsed);
            
            return sortedByVersion
                   .Where(kvp => kvp.Value != null && kvp.Value.Any())
                   .OrderByDescending(kvp => kvp.Value.First().GetUsageDetails().First().DateTime);
        }
    }
}