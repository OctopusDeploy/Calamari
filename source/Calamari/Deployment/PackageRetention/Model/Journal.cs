using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IRetentionAlgorithm retentionAlgorithm;
        readonly IFreeSpaceChecker freeSpaceChecker;
        
        public Journal(IJournalRepositoryFactory repositoryFactory,
                       ILog log,
                       ICalamariFileSystem fileSystem,
                       IRetentionAlgorithm retentionAlgorithm,
                       IVariables variables,
                       IFreeSpaceChecker freeSpaceChecker)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
            this.fileSystem = fileSystem;
            this.retentionAlgorithm = retentionAlgorithm;
            this.variables = variables;
            this.freeSpaceChecker = freeSpaceChecker;
        }

        public void RegisterPackageUse(IVariables variables)
        {
            if (!IsRetentionEnabled())
                return;

            RegisterPackageUse(new PackageIdentity(variables), new ServerTaskId(variables));
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId)
        {
            if (!IsRetentionEnabled())
                return;

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

        bool IsRetentionEnabled()
        {
            var tentacleHome = variables.Get(TentacleVariables.Agent.TentacleHome);
            return variables.IsPackageRetentionEnabled() && tentacleHome != null;
        }

        public bool HasLock(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                       && entry.PackageLocks.HasLock();
            }
        }

        public void ApplyRetention(string directory)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                var requiredSpace = freeSpaceChecker.GetRequiredSpace(directory);
                
                var packagesToRemove = retentionAlgorithm.GetPackagesToRemove(repository.GetAllJournalEntries(), requiredSpace);

                foreach (var package in packagesToRemove)
                {
                    if (string.IsNullOrWhiteSpace(package.Path) || !fileSystem.FileExists(package.Path)) return;

                    Log.Info($"Removing package file '{package.Path}'");
                    fileSystem.DeleteFile(package.Path, FailureOptions.IgnoreFailure);

                    repository.RemovePackageEntry(package);
                }
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
            throw new NotImplementedException();
        }
    }
}