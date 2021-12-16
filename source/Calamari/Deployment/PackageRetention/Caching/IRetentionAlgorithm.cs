using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public interface IRetentionAlgorithm
    {
        IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries, long spaceNeeded);
    }
}