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
    }
}