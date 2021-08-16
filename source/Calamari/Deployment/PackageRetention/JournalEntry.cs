using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Deployment.PackageRetention
{

    public class Journal
    {
        readonly List<JournalEntry> journalEntries = new List<JournalEntry>();

        public void AddOrUpdateEntry(string deploymentId, string package)
        {

        }


        public bool HasLock(string package)
        {
            return journalEntries.Any(e => e.Package == package && e.HasLock());
        }
    }

    public class JournalEntry
    {
        public string Package { get; }
        readonly Dictionary<string, PackageLock> packageLocks;

        public JournalEntry(string package, List<PackageLock> packageLocks = null)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            Package = package;

            this.packageLocks = packageLocks == null
                ? new Dictionary<string, PackageLock>()
                : packageLocks.ToDictionary(l => l.DeploymentId, l => l);
        }

        public void AddLock(string deploymentId)
        {
            if (packageLocks.ContainsKey(deploymentId))
            {
                packageLocks[deploymentId].UpdateLockedWhen();
            }
            else
            {
                packageLocks.Add(deploymentId, new PackageLock(deploymentId));
            }
        }

        public void RemoveLock(string deploymentId)
        {
            if (packageLocks.ContainsKey(deploymentId))
            {
                packageLocks.Remove(deploymentId);
            }
        }

        public bool HasLock() => packageLocks.Count > 0;
    }

    public class PackageLock
    {
        public DateTime LockedWhen { get; private set; }
        public string DeploymentId { get; set; }

        public PackageLock(string deploymentId)
        {
            this.DeploymentId = deploymentId;
        }

        public void UpdateLockedWhen()
        {
            LockedWhen = DateTime.Now;
        }
    }
}