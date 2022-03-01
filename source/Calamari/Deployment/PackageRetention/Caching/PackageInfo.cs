using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PackageInfo
    {
        public JournalEntry Entry { get; set; }
        public CacheAge Age { get; }
        public int HitCount { get; }
        public int NewerVersionCount { get; }
        public long FileSizeBytes { get; }

        public PackageInfo(JournalEntry entry, IEnumerable<JournalEntry> entries)
        {
            Entry = entry;
            FileSizeBytes = entry.FileSizeBytes;
            Age = GetOldestCacheAgeAtUsage(entry);
            HitCount = GetHitCount(entry);
            NewerVersionCount = GetNumberOfNewerVersions(entry, entries);
        }

        static CacheAge GetOldestCacheAgeAtUsage(JournalEntry entry)
        {
            return entry.GetUsageDetails().Min(ud => ud.CacheAgeAtUsage);
        }

        static int GetHitCount(JournalEntry entry)
        {
            return entry.GetUsageDetails().Count();
        }

        static int GetNumberOfNewerVersions(JournalEntry entry, IEnumerable<JournalEntry> entries)
        {
            return entries.Count(e => e.Package.PackageId == entry.Package.PackageId && e.Package.Version.CompareTo(entry.Package.Version) > 0);
        }
    }
}