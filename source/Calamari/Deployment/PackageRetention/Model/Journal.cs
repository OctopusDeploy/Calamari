using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Repositories;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IJournal
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly ILog log;

        public Journal(IJournalRepositoryFactory repositoryFactory, ILog log)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
        }

        public void RegisterPackageUse(string packageID, IVersion version, string serverTaskID)
        {
            RegisterPackageUse(new PackageIdentity(packageID, version.OriginalString), new ServerTaskID(serverTaskID));
        }
        public void RegisterPackageUse(IVariables variables)
        {
            RegisterPackageUse(new PackageIdentity(variables), new ServerTaskID(variables));
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskID serverTaskID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(package, out var entry))
            {
                entry.PackageUsage.AddUsage(serverTaskID);
                entry.PackageLocks.AddLock(serverTaskID);
            }
            else
            {
                entry = new JournalEntry(package);
                entry.PackageUsage.AddUsage(serverTaskID);
                entry.PackageLocks.AddLock(serverTaskID);
                repository.AddJournalEntry(entry);
            }

            log.Verbose($"Registered package use/lock for {package} and task {serverTaskID}");

            repository.Commit();
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskID serverTaskID)
        {
            var repository = repositoryFactory.CreateJournalRepository();

            if (repository.TryGetJournalEntry(package, out var entry))
            {
                entry.PackageLocks.RemoveLock(serverTaskID);
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