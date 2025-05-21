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

            return entries.Select(je => new
                          {
                              journalEntry = je,
                              LastUsed = je.GetUsageDetails().Max(u => u.DateTime)
                          })
                          .OrderBy(e => e.LastUsed)
                          .Select(e => e.journalEntry);
        }
    }
}