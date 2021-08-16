using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using SharpCompress.Common;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
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
}