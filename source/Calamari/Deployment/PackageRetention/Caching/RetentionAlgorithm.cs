using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class RetentionAlgorithm : IRetentionAlgorithm
    {
        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceNeeded)
        {
            return journalEntries.Select(pair => pair.Package);
        }
    }
}