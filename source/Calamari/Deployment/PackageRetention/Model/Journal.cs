using System;
using System.Collections.Generic;
using System.Linq;
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

        public void RegisterPackageUse()
        {
            try
            {
                RegisterPackageUse(new PackageIdentity(variables), new ServerTaskId(variables));
            }
            catch (Exception ex)
            {
                log.Error($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
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
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.RemoveLock(deploymentTaskId);
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
            return variables.IsPackageRetentionEnabled();
        }

        public bool HasLock(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                       && entry.HasLock();
            }
        }

        public void ApplyRetention(string directory)
        {
            if (IsRetentionEnabled())
            {
                try
                {
                    using (var repository = repositoryFactory.CreateJournalRepository())
                    {
                        var requiredSpace = freeSpaceChecker.GetRequiredSpace(directory);
                        var packagesToRemove = retentionAlgorithm.GetPackagesToRemove(repository.GetAllJournalEntries(), requiredSpace);

                        foreach (var package in packagesToRemove)
                        {
                            if (string.IsNullOrWhiteSpace(package?.Path) || !fileSystem.FileExists(package.Path))
                            {
                                log.Warn($"Package at {package?.Path} not found.");
                                continue;
                            }

                            Log.Info($"Removing package file '{package.Path}'");
                            fileSystem.DeleteFile(package.Path, FailureOptions.IgnoreFailure);

                            repository.RemovePackageEntry(package);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }
            else
            {
                freeSpaceChecker.EnsureDiskHasEnoughFreeSpace(directory);
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

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }
    }
}