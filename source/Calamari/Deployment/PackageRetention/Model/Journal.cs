using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Repositories;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IJournal
    {
        readonly IJournalRepositoryFactory repositoryFactory;

        public Journal(IJournalRepositoryFactory repositoryFactory)
        {
            this.repositoryFactory = repositoryFactory;
        }

        public void RegisterPackageUse(string packageID, IVersion version, string deploymentID)
        {
            RegisterPackageUse(new PackageIdentity(packageID, version.OriginalString), new ServerTaskID(deploymentID));
        }
        public void RegisterPackageUse(IVariables variables)
        {
            RegisterPackageUse(new PackageIdentity(variables), new ServerTaskID(variables));
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskID deploymentID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(package, out var entry))
            {
                entry.PackageUsage.AddUsage(deploymentID);
                entry.PackageLocks.AddLock(deploymentID);
            }
            else
            {
                entry = new JournalEntry(package);
                entry.PackageUsage.AddUsage(deploymentID);
                entry.PackageLocks.AddLock(deploymentID);
                repository.AddJournalEntry(entry);
            }

            repository.Commit();
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskID deploymentID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(package, out var entry))
            {
                entry.PackageLocks.RemoveLock(deploymentID);
            }   //TODO: Else exception?

        }

        public bool HasLock(PackageIdentity package)
        {
            return repositoryFactory.CreateJournalRepository()
                                     .TryGetJournalEntry(package, out var entry)
                   && entry.PackageLocks.HasLock();
        }

        public IEnumerable<DateTime> GetUsage(PackageIdentity package)
        {
            return repositoryFactory.CreateJournalRepository()
                                     .TryGetJournalEntry(package, out var entry)
                ? entry.PackageUsage.GetUsageDetails()
                : new DateTime[0];
        }

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }
    }
}