using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using SharpCompress.Common;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention
{
    public class Journal
    {
        readonly IJournalRepositoryFactory repositoryFactory;

        public Journal(IJournalRepositoryFactory repositoryFactory)
        {
            this.repositoryFactory = repositoryFactory;
        }

        public void RegisterPackageUse(PackageID packageID, DeploymentID deploymentID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(packageID, out var entry))
            {
                entry.PackageUsage.AddUsage(deploymentID);
                entry.PackageLocks.AddLock(deploymentID);
            }
            else
            {
                entry = new JournalEntry(packageID);
                entry.PackageUsage.AddUsage(deploymentID);
                entry.PackageLocks.AddLock(deploymentID);
                repository.AddJournalEntry(entry);
            }

            repository.Commit();
        }

        public void DeregisterPackageUse(PackageID packageID, DeploymentID deploymentID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(packageID, out var entry))
            {
                entry.PackageLocks.RemoveLock(deploymentID);
            }   //TODO: Else exception?

        }

        public bool HasLock(PackageID packageID)
        {
            return repositoryFactory.CreateJournalRepository()
                                     .TryGetJournalEntry(packageID, out var entry)
                   && entry.PackageLocks.HasLock();
        }

        public IEnumerable<DateTime> GetUsage(PackageID packageID)
        {
            return repositoryFactory.CreateJournalRepository()
                                     .TryGetJournalEntry(packageID, out var entry)
                ? entry.PackageUsage.GetUsageDetails()
                : new DateTime[0];
        }

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }
    }

    public class JournalEntry
    {
        public PackageID PackageID { get; }
        public PackageLocks PackageLocks { get; }
        public PackageUsage PackageUsage { get; }

        public JournalEntry(PackageID packageID, PackageLocks packageLocks = null, PackageUsage packageUsage = null)
        {
            PackageID = packageID ?? throw new ArgumentNullException(nameof(packageID));
            PackageLocks = packageLocks ?? new PackageLocks();
            PackageUsage = packageUsage ?? new PackageUsage();
        }
    }

    public class PackageUsage
    {
        readonly Dictionary<DeploymentID, List<DateTime>> usages;

        internal PackageUsage(Dictionary<DeploymentID, List<DateTime>> usageRecord = null)
        {
            usages = usageRecord ?? new Dictionary<DeploymentID, List<DateTime>>();
        }

        public void AddUsage(DeploymentID deploymentID)
        {
            if (!usages.ContainsKey(deploymentID))
                usages.Add(deploymentID, new List<DateTime>());

            usages[deploymentID].Add(DateTime.Now);
        }

        public IEnumerable<DateTime> GetUsageDetails()
        {
            return usages.SelectMany(u => u.Value);
        }

        /*
        public IEnumerable<(DateTime When, int Count)> GetUsageCountsWhens()
        {
            return usages.SelectMany(u => u.Value)
                         .GroupBy(i => i)
                         .Select(group => (When: group.Key, Count: group.Count()));
        }      */
    }

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