using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public static class JournalEntrySorter
    {
        public static IEnumerable<JournalEntry> LeastRecentlyUsed(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();

            return entries.OrderBy(je => je.GetUsageDetails().Max(u => u.DateTime));
        }
        
        public static IOrderedEnumerable<KeyValuePair<string, IEnumerable<JournalEntry>>> LeastRecentlyUsedByVersion(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();
            
            var sortedByVersion = entries
                                  .GroupBy(je => je.Package.PackageId.ToString())
                                  .ToDictionary(k => k.Key, LeastRecentlyUsed);
            
            return sortedByVersion
                .Where(kvp => kvp.Value != null && kvp.Value.Any())
                .OrderBy(kvp => kvp.Value.First().GetUsageDetails().First().DateTime);
        }
    }
}