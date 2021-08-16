using System;
using System.Collections.Generic;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageLocks
    {
        readonly Dictionary<DeploymentID, DateTime> packageLocks;

        internal PackageLocks(Dictionary<DeploymentID, DateTime> packageLocks)
        {
            this.packageLocks = packageLocks ?? new Dictionary<DeploymentID, DateTime>();
        }

        public PackageLocks() : this(new Dictionary<DeploymentID, DateTime>())
        {
        }

        public void AddLock(DeploymentID deploymentID)
        {
            if (packageLocks.ContainsKey(deploymentID))
                packageLocks[deploymentID] = DateTime.Now;
            else
                packageLocks.Add(deploymentID, DateTime.Now);
        }

        public void RemoveLock(DeploymentID deploymentID)
        {
            packageLocks.Remove(deploymentID);
        }

        public bool HasLock() => packageLocks.Count > 0;
    }
}