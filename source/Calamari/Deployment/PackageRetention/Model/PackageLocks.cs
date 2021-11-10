using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageLocks
    {
        [JsonProperty]
        readonly Dictionary<ServerTaskId, DateTime> packageLocks;

        [JsonConstructor]
        internal PackageLocks(Dictionary<ServerTaskId, DateTime> packageLocks)
        {
            this.packageLocks = packageLocks ?? new Dictionary<ServerTaskId, DateTime>();
        }

        public PackageLocks() : this(new Dictionary<ServerTaskId, DateTime>())
        {
        }

        public void AddLock(ServerTaskId deploymentTaskId)
        {
            packageLocks[deploymentTaskId] = DateTime.Now;
        }

        public void RemoveLock(ServerTaskId deploymentTaskId)
        {
            packageLocks.Remove(deploymentTaskId);
        }

        public bool HasLock() => packageLocks.Count > 0;
    }
}