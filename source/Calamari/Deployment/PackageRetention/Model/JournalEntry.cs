using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;
using NuGet;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class JournalEntry
    {
        public PackageIdentity Package { get; }
        
        [JsonProperty]
        readonly PackageUsages usages;

        [JsonProperty]
        readonly PackageLocks locks;

        [JsonConstructor]
        public JournalEntry(PackageIdentity package, PackageLocks packageLocks = null, PackageUsages packageUsages = null)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            locks = packageLocks ?? new PackageLocks();
            usages = packageUsages ?? new PackageUsages();
            Package.UpdatePackageSize();
        }

        public void AddUsage(ServerTaskId deploymentTaskId, CacheAge cacheAge)
        {
            usages.Add(new UsageDetails(deploymentTaskId, cacheAge));
        }

        public void AddLock(ServerTaskId deploymentTaskId, CacheAge cacheAge)
        {
            locks.Add(new UsageDetails(deploymentTaskId, cacheAge));
        }

        public void RemoveLock(ServerTaskId deploymentTaskId)
        {
            locks.RemoveAll(l => l.DeploymentTaskId == deploymentTaskId);
        }

        public bool HasLock() => locks.Count > 0;

        public IEnumerable<UsageDetails> GetUsageDetails() => usages;
        public IEnumerable<UsageDetails> GetLockDetails() => locks;
    }
}