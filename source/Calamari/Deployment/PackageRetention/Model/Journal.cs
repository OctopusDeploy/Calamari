using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using SharpCompress.Common;
using Calamari.Deployment.PackageRetention.Repositories;
using Newtonsoft.Json.Bson;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal
    {
        readonly IJournalRepositoryFactory repositoryFactory;

        public Journal(IJournalRepositoryFactory repositoryFactory)
        {
            this.repositoryFactory = repositoryFactory;
        }

        public void RegisterPackageUse(string packageID, IVersion version, string deploymentID)
        {
            RegisterPackageUse(new PackageIdentity(packageID, version.OriginalString), new DeploymentID(deploymentID));
        }
        public void RegisterPackageUse(IVariables variables)
        {
            RegisterPackageUse(new PackageIdentity(variables), new DeploymentID(variables));
        }

        public void RegisterPackageUse(PackageIdentity package, DeploymentID deploymentID)
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

        public void DeregisterPackageUse(PackageIdentity package, DeploymentID deploymentID)
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