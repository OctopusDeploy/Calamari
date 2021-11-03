using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageLocks
    {
        [JsonProperty]
        readonly Dictionary<ServerTaskID, DateTime> packageLocks;

        [JsonConstructor]
        internal PackageLocks(Dictionary<ServerTaskID, DateTime> packageLocks)
        {
            this.packageLocks = packageLocks ?? new Dictionary<ServerTaskID, DateTime>();
        }

        public PackageLocks() : this(new Dictionary<ServerTaskID, DateTime>())
        {
        }

        public void AddLock(ServerTaskID deploymentTaskID)
        {
            if (packageLocks.ContainsKey(deploymentTaskID))
                packageLocks[deploymentTaskID] = DateTime.Now;
            else
                packageLocks.Add(deploymentTaskID, DateTime.Now);
        }

        public void RemoveLock(ServerTaskID deploymentTaskID)
        {
            packageLocks.Remove(deploymentTaskID);
        }

        public bool HasLock() => packageLocks.Count > 0;
    }
}