using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public static class JournalEntrySorter
    {
        static IEnumerable<JournalEntry> MostRecentlyUsed(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();

            return entries.OrderByDescending(je => je.GetUsageDetails().Max(u => u.DateTime));
        }
        
        public static IOrderedEnumerable<KeyValuePair<string, IEnumerable<JournalEntry>>> MostRecentlyUsedByPackageId(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(e => !e.HasLock()).ToList();
            
            var orderedJournalEntriesByPackageId = entries
                                             .GroupBy(je => je.Package.PackageId.ToString())
                                             .ToDictionary(jeg => jeg.Key, MostRecentlyUsed);
            
            return orderedJournalEntriesByPackageId
                   .Where(kvp => kvp.Value != null && kvp.Value.Any())
                   .OrderByDescending(kvp => kvp.Value.First().GetUsageDetails().Max(u => u.DateTime));
        }

        public static Dictionary<string, IEnumerable<PackageIdentity>> MostRecentlyUsedByVersion(IEnumerable<JournalEntry> journalEntries)
        {
            var entries = journalEntries.Where(je => !je.HasLock()).ToList();

            var packageGroups = entries.GroupBy(je => je.Package.PackageId.ToString());

            var journalEntriesByPackageAndVersion = new Dictionary<string, IEnumerable<PackageIdentity>>();

            foreach (var packageGroup in packageGroups)
            {
                // PackageIdentity represents package ID and version
                var journalEntriesByMostRecentVersion = packageGroup.OrderByDescending(je => je.GetUsageDetails().Max(u => u.DateTime)).Select(je => je.Package).ToArray();
                journalEntriesByPackageAndVersion.Add(packageGroup.Key, journalEntriesByMostRecentVersion);
            }

            return journalEntriesByPackageAndVersion;

        }
    }
}