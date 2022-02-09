using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FirstInFirstOutCacheAlgorithm : RetentionAlgorithmBase
    {
        public override IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceRequired)
        {
            journalEntries = journalEntries.Where(e => !e.HasLock());
            var packagesByAge = OrderPackagesByOldestFirst(journalEntries);

            var diskSpaceFound = 0L;
            var packagesToRemove = new List<PackageIdentity>();

            foreach (var package in packagesByAge)
            {
                diskSpaceFound += package.FileSizeBytes;
                packagesToRemove.Add(package.Package);

                if (diskSpaceFound >= spaceRequired) return packagesToRemove;
            }

            //If we haven't returned yet, then we can't clear enough space in the cache to continue.
            throw new InsufficientCacheSpaceException(diskSpaceFound, spaceRequired);
        }


        IEnumerable<JournalEntry> OrderPackagesByOldestFirst(IEnumerable<JournalEntry> journalEntries)
        {
            return journalEntries.OrderBy(GetCacheAgeAtFirstPackageUse);
        }
    }
}