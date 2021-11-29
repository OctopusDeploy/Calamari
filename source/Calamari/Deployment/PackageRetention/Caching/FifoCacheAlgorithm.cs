using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FifoCacheAlgorithm : RetentionAlgorithmBase
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
                packagesToRemove.Add(package);

                if (diskSpaceFound >= spaceRequired) return packagesToRemove;
            }

            //If we haven't returned yet, then we can't clear enough space in the cache to continue.
            throw new InsufficientCacheSpaceException();   //TODO improve the info in this exception
        }


        IEnumerable<PackageIdentity> OrderPackagesByOldestFirst(IEnumerable<JournalEntry> journalEntries)
        {
            //TODO: optimise this.
            return journalEntries.OrderBy(GetCacheAgeAtFirstPackageUse).Select(je => je.Package);
        }
    }
}