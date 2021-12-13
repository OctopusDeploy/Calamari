using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PackageInfo
    {
        public PackageIdentity Package { get; set; }

        public CacheAge Age { get; }
        public int HitCount { get; }
        public int NewerVersionCount { get; }

        public PackageInfo(JournalEntry entry, IEnumerable<JournalEntry> entries)
        {
            Package = entry.Package;
            Age = GetAge(entry);
            HitCount = GetHitCount(entry);
            NewerVersionCount = GetNumberOfNewerVersions(entry, entries);
        }

        CacheAge GetAge(JournalEntry entry)
        {
            return entry.GetUsageDetails().Min(ud => ud.CacheAgeAtUsage);
        }

        int GetHitCount(JournalEntry entry)
        {
            return entry.GetUsageDetails().Count();
        }

        int GetNumberOfNewerVersions(JournalEntry entry, IEnumerable<JournalEntry> entries)
        {
            return entries.Count(e => e.Package.PackageId == entry.Package.PackageId && e.Package.Version.CompareTo(entry.Package.Version) > 0);
        }
    }
}