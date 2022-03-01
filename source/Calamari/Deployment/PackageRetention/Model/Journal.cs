﻿using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly IRetentionAlgorithm retentionAlgorithm;
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IFreeSpaceChecker freeSpaceChecker;
        public const string PackageRetentionCacheSizeInMegaBytesVariable = "OctopusPackageRetentionCacheSizeInMegaBytes";

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

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    repository.Cache.IncrementCacheAge();
                    var age = repository.Cache.CacheAge;

                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.Package.UpdatePackageSize();
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

        public void ApplyRetention(string directory)
        {
            log.Verbose($"Applying package retention for folder '{directory}'");
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    var cacheSizeInMB = variables.Get(PackageRetentionCacheSizeInMegaBytesVariable);
                    var cacheSpaceRequired = 0L;

                    if (decimal.TryParse(cacheSizeInMB, out var cacheSizeMB))
                    {
                        var cacheSizeBytes = (long)Math.Round(cacheSizeMB * 1024L * 1024L);
                        var cacheSpaceRemaining = GetCacheSpaceRemaining(cacheSizeBytes);
                        var requiredSpaceInBytes = (long)freeSpaceChecker.GetRequiredSpaceInBytes();
                        cacheSpaceRequired = Math.Max(0, requiredSpaceInBytes - cacheSpaceRemaining);

                        log.Verbose($"Cache size is {cacheSizeMB} MB, remaining space is {cacheSpaceRemaining / 1024D / 1024D:N} MB, with {cacheSpaceRequired / 1024D / 1024:N} MB required to be freed.");
                    }

                    var requiredSpace = Math.Max(cacheSpaceRequired, (long)freeSpaceChecker.GetSpaceRequiredToBeFreed(directory));
                    log.Verbose($"Total space required to be freed is {requiredSpace / 1024D / 1024:N} MB.");
                    var packagesToRemove = retentionAlgorithm.GetPackagesToRemove(repository.GetAllJournalEntries(), requiredSpace);

                    foreach (var package in packagesToRemove)
                    {
                        if (string.IsNullOrWhiteSpace(package?.Path) || !fileSystem.FileExists(package.Path))
                        {
                            log.Info($"Package at {package?.Path} not found.");
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

        public bool TryGetVersionFormat(PackageId packageId, string version, VersionFormat defaultFormat, out VersionFormat versionFormat)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                //Try to match on version, if that doesn't work, then just get another entry.
                var entryMatchingVersion = repository.GetJournalEntries(packageId).FirstOrDefault(je => je.Package.Version.OriginalString == version)
                                           ?? repository.GetJournalEntries(packageId).FirstOrDefault();

                versionFormat = entryMatchingVersion == null ? defaultFormat : entryMatchingVersion.Package.Version.Format;

                return entryMatchingVersion != null;
            }
        }

        public bool TryGetVersionFormat(PackageId packageId, ServerTaskId deploymentTaskID, VersionFormat defaultFormat, out VersionFormat format)
        {
            //We can call this if we don't know the package version format from variables - if this isn't the first time this package has been referenced by this server task, then we should be able to get it from an earlier use (eg from FindPackageCommand)
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.GetJournalEntries(packageId, deploymentTaskID)
                                 .TryGetFirstValidVersionFormat(defaultFormat, out format);
            }
        }

        long GetCacheSpaceRemaining(long cacheSize)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return cacheSize - repository.GetAllJournalEntries().Sum(e => Math.Max(e.Package.FileSizeBytes, 0));
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