using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public abstract class RetentionAlgorithmBase : IRetentionAlgorithm
    {
        public abstract IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceNeeded);

        /// <summary>
        /// Cache age is a record of the number of packages accesses.
        /// Note that we're not returning the age of the package use - so a smaller number means an older use.
        /// </summary>
        internal int GetCacheAgeAtLastPackageUse(PackageIdentity package, IEnumerable<JournalEntry> journalEntries)
        {
            return GetCacheAgeAtLastPackageUse(journalEntries.FirstOrDefault(e => e.Package == package));
        }

        internal int GetCacheAgeAtFirstPackageUse(PackageIdentity package, IEnumerable<JournalEntry> journalEntries)
        {
            return GetCacheAgeAtFirstPackageUse(journalEntries.FirstOrDefault(e => e.Package == package));
        }

        internal int GetCacheAgeAtLastPackageUse(JournalEntry entry)
        {
            return entry?.GetUsageDetails()?.Max(m => m.CacheAge.Value)
                   ?? int.MinValue;
        }

        internal int GetCacheAgeAtFirstPackageUse(JournalEntry entry)
        {
            return entry?.GetUsageDetails()?.Min(m => m.CacheAge.Value)
                   ?? int.MaxValue;
        }

        internal int GetNewerVersionCount(PackageIdentity package, IEnumerable<JournalEntry> journalEntries)
        {
            var entriesForPackageId = journalEntries.Where(e => e.Package.PackageId == package.PackageId);
            return entriesForPackageId.Count(e => e.Package.Version.CompareTo(package.Version) == 1);
        }
    }
}