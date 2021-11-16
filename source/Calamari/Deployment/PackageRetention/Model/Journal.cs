using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse, IDisposable
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly ILog log;

        public Journal(IJournalRepositoryFactory repositoryFactory, ILog log)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
        }

        public void RegisterPackageUse(IVariables variables)
        {
            RegisterPackageUse(new PackageIdentity(variables), new ServerTaskId(variables));
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {

                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.PackageUsage.AddUsage(deploymentTaskId, 0);   //TODO: fix this to use the actual age.
                        entry.PackageLocks.AddLock(deploymentTaskId);
                    }
                    else
                    {
                        entry = new JournalEntry(package);
                        entry.PackageUsage.AddUsage(deploymentTaskId, 0);
                        entry.PackageLocks.AddLock(deploymentTaskId);
                        repository.AddJournalEntry(entry);
                    }

#if DEBUG
                    log.Verbose($"Registered package use/lock for {package} and task {deploymentTaskId}");
#endif

                    repository.Commit();
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Error($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.PackageLocks.RemoveLock(serverTaskId);
                        repository.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Error($"Unable to deregister package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public bool HasLock(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                       && entry.PackageLocks.HasLock();
            }
        }


        //*** Cache functions from here - maybe move into separate class and/or interface? - MC ***
        public IEnumerable<IUsageDetails> GetUsage(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                    ? entry.PackageUsage.GetUsageDetails()
                    : new UsageDetails[0];
            }
        }

        public int GetUsageCount(PackageIdentity package)
        {
            return GetUsage(package).Count();
        }

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Age is the number of other packages which have been registered or accessed since this one.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public int GetPackageAge(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                if (!repository.TryGetJournalEntry(package, out var journalEntry)) return int.MaxValue; //TODO: it doesn't exist, so for now return int.MAx value. Later => fail?
                 /*
                var oldestUsageDate = journalEntry.PackageUsage.GetUsageDetails().OrderBy(dt => dt).FirstOrDefault();
                var entries = repository.GetAllJournalEntries();
                var allUsageDates = entries.SelectMany(je => je.PackageUsage.GetUsageDetails());
                var age = allUsageDates.Count(d => d > oldestUsageDate);
               */
                 return 1; //TODO: return the actual date
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}