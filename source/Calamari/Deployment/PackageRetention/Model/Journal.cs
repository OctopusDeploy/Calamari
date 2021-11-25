using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse
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

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {

                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.PackageUsage.AddUsage(serverTaskId);
                        entry.PackageLocks.AddLock(serverTaskId);
                    }
                    else
                    {
                        entry = new JournalEntry(package);
                        entry.PackageUsage.AddUsage(serverTaskId);
                        entry.PackageLocks.AddLock(serverTaskId);
                        repository.AddJournalEntry(entry);
                    }

#if DEBUG
                    log.Verbose($"Registered package use/lock for {package} and task {serverTaskId}");
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

        public IEnumerable<DateTime> GetUsage(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                    ? entry.PackageUsage.GetUsageDetails()
                    : new DateTime[0];
            }
        }

        public void ExpireStaleLocks()
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    var entries = repository.GetEntriesWithStaleTasks();

                    foreach (var (entry, staleTasks) in entries)
                    {
                        foreach (var taskId in staleTasks)
                        {
                            entry.PackageLocks.RemoveLock(taskId);   
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                log.Error($"Unable to expire stale locks.{Environment.NewLine}{ex.ToString()}");
            }
        }
    }
}