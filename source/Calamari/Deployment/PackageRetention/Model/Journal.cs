using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly IVariables variables;
        readonly ILog log;

        public Journal(IJournalRepositoryFactory repositoryFactory, IVariables variables, ILog log)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
            this.variables = variables;
        }

        public void RegisterPackageUse()
        {
            RegisterPackageUse(out _);
        }

        public void RegisterPackageUse(out bool packageRegistered)
        {
            try
            {
                RegisterPackageUse(new PackageIdentity(variables), new ServerTaskId(variables), out packageRegistered);
            }
            catch (Exception ex)
            {
                packageRegistered = false;
                log.Error($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
            RegisterPackageUse(package, deploymentTaskId, out _);
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId, out bool packageRegistered)
        {
            packageRegistered = false;

            if (!IsRetentionEnabled())
                return;

            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    repository.Cache.IncrementCacheAge();
                    var age = repository.Cache.CacheAge;

                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.AddUsage(deploymentTaskId, age);
                        entry.AddLock(deploymentTaskId, age);
                    }
                    else
                    {
                        entry = new JournalEntry(package);
                        entry.AddUsage(deploymentTaskId, age);
                        entry.AddLock(deploymentTaskId, age);
                        repository.AddJournalEntry(entry);
                    }

#if DEBUG
                    log.Verbose($"Registered package use/lock for {package} and task {deploymentTaskId}");
#endif

                    repository.Commit();
                    packageRegistered = true;
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Error($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
            DeregisterPackageUse(package, deploymentTaskId, out _);
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId, out bool packageDeregistered)
        {
            packageDeregistered = false;

            if (!IsRetentionEnabled())
                return;

            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.RemoveLock(deploymentTaskId);
                        repository.Commit();
                        packageDeregistered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Error($"Unable to deregister package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        bool IsRetentionEnabled()
        {
            var enabled = variables.IsPackageRetentionEnabled();
            log.Verbose($"Package retention is {(enabled ? "enabled" : "disabled")}.");
            return enabled;
        }

        public bool HasLock(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                       && entry.HasLock();
            }
        }

        //*** Cache functions from here - maybe move into separate class and/or interface? - MC ***
        public IEnumerable<IUsageDetails> GetUsage(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                    ? entry.GetUsageDetails()
                    : new UsageDetails[0];
            }
        }

        public int GetUsageCount(PackageIdentity package)
        {
            return GetUsage(package).Count();
        }

        public void ExpireStaleLocks(TimeSpan timeBeforeExpiration)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    foreach (var entry in repository.GetAllJournalEntries())
                    {
                        var locks = entry.GetLockDetails();
                        var staleLocks = locks.Where(u => u.DateTime.Add(timeBeforeExpiration) <= DateTime.Now);

                        foreach (var staleLock in staleLocks)
                        {
                            entry.RemoveLock(staleLock.DeploymentTaskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Unable to expire stale package locks.{Environment.NewLine}{ex.ToString()}");
            }
        }
    }
}