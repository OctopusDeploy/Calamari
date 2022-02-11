using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageCache
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly IRetentionAlgorithm retentionAlgorithm;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IFreeSpaceChecker freeSpaceChecker;

        public Journal(IJournalRepositoryFactory repositoryFactory,
                       ILog log,
                       ICalamariFileSystem fileSystem,
                       IRetentionAlgorithm retentionAlgorithm,
                       IFreeSpaceChecker freeSpaceChecker)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
            this.fileSystem = fileSystem;
            this.retentionAlgorithm = retentionAlgorithm;
            this.freeSpaceChecker = freeSpaceChecker;
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId, long packageSizeBytes)
        {
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
                        entry = new JournalEntry(package, packageSizeBytes);
                        entry.AddUsage(deploymentTaskId, age);
                        entry.AddLock(deploymentTaskId, age);
                        repository.AddJournalEntry(entry);
                    }

                    log.Verbose($"Registered package use/lock for {package} and task {deploymentTaskId}");

                    repository.Commit();
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Info($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
            try
            {
                log.Verbose($"Deregistering package lock for {package} and task {deploymentTaskId}");

                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.RemoveLock(deploymentTaskId);
                        repository.Commit();

                        log.Verbose($"Successfully deregistered package lock for {package} and task {deploymentTaskId}");
                    }
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Info($"Unable to deregister package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public bool HasLock(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                       && entry.HasLock();
            }
        }

        public void ApplyRetention(string directory, int cacheSizeInMegaBytes)
        {
            log.Verbose($"Applying package retention for folder '{directory}'");
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    var cacheSpaceRequired = 0L;

                    if (cacheSizeInMegaBytes > 0)
                    {
                        var cacheSizeBytes = (long)Math.Round((decimal)(cacheSizeInMegaBytes * 1024 * 1024));
                        var cacheSpaceRemaining = GetCacheSpaceRemaining(cacheSizeBytes);
                        var requiredSpaceInBytes = (long)freeSpaceChecker.GetRequiredSpaceInBytes();
                        cacheSpaceRequired = Math.Max(0, requiredSpaceInBytes - cacheSpaceRemaining);

                        log.Verbose($"Cache size is {cacheSizeInMegaBytes} MB, remaining space is {cacheSpaceRemaining / 1024D / 1024D:N} MB, with {cacheSpaceRequired / 1024D / 1024:N} MB required to be freed.");
                    }

                    var requiredSpace = Math.Max(cacheSpaceRequired, (long)freeSpaceChecker.GetSpaceRequiredToBeFreed(directory));
                    log.Verbose($"Total space required to be freed is {requiredSpace / 1024D / 1024:N} MB.");
                    var packagesToRemove = retentionAlgorithm.GetPackagesToRemove(repository.GetAllJournalEntries(), requiredSpace);

                    foreach (var package in packagesToRemove)
                    {
                        if (string.IsNullOrWhiteSpace(package.Path.Value) || !fileSystem.FileExists(package.Path.Value))
                        {
                            log.Info($"Package at {package?.Path} not found.");
                            continue;
                        }

                        Log.Info($"Removing package file '{package.Path}'");
                        fileSystem.DeleteFile(package.Path.Value, FailureOptions.IgnoreFailure);

                        repository.RemovePackageEntry(package);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Info(ex.Message);
            }
        }

        public IEnumerable<IUsageDetails> GetUsage(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.TryGetJournalEntry(package, out var entry)
                    ? entry.GetUsageDetails()
                    : new UsageDetails[0];
            }
        }

        long GetCacheSpaceRemaining(long cacheSize)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return cacheSize - repository.GetAllJournalEntries().Sum(e => Math.Max(e.FileSizeBytes, 0));
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
                log.Info($"Unable to expire stale package locks.{Environment.NewLine}{ex.ToString()}");
            }
        }
    }
}