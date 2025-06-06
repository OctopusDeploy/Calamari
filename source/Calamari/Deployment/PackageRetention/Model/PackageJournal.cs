﻿using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageJournal : IManagePackageCache
    {
        readonly IJournalRepository journalRepository;
        readonly IRetentionAlgorithm[] retentionAlgorithms;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;

        public PackageJournal(IJournalRepository journalRepository,
                              ILog log,
                              ICalamariFileSystem fileSystem,
                              IEnumerable<IRetentionAlgorithm> retentionAlgorithms,
                              ISemaphoreFactory semaphoreFactory)
        {
            this.journalRepository = journalRepository;
            this.log = log;
            this.fileSystem = fileSystem;
            this.retentionAlgorithms = retentionAlgorithms.ToArray();
            this.semaphoreFactory = semaphoreFactory;
        }

        public void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId, ulong packageSizeBytes)
        {
            try
            {
                using (AcquireSemaphore())
                {
                    journalRepository.Load();
                    journalRepository.Cache.IncrementCacheAge();
                    var age = journalRepository.Cache.CacheAge;
                    if (journalRepository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.AddUsage(deploymentTaskId, age);
                        entry.AddLock(deploymentTaskId, age);
                    }
                    else
                    {
                        entry = new JournalEntry(package, packageSizeBytes);
                        entry.AddUsage(deploymentTaskId, age);
                        entry.AddLock(deploymentTaskId, age);
                        journalRepository.AddJournalEntry(entry);
                    }

                    log.Verbose($"Registered package use/lock for {package} and task {deploymentTaskId}");
                    journalRepository.Commit();
                }
            }
            catch (Exception ex)
            {
                //We need to ensure that an issue with the journal doesn't interfere with the deployment.
                log.Info($"Unable to register package use for retention.{Environment.NewLine}{ex.ToString()}");
            }
        }

        public void RemoveAllLocks(ServerTaskId serverTaskId)
        {
            using (AcquireSemaphore())
            {
                log.Verbose($"Releasing package locks for task {serverTaskId}");
                journalRepository.Load();
                journalRepository.RemoveAllLocks(serverTaskId);
                journalRepository.Commit();
            }
        }

        public void ApplyRetention()
        {
            try
            {
                using (AcquireSemaphore())
                {
                    journalRepository.Load();
                    var packagesToRemove = retentionAlgorithms.SelectMany(algorithm => algorithm.GetPackagesToRemove(journalRepository.GetAllJournalEntries()));
                    foreach (var package in packagesToRemove)
                    {
                        if (string.IsNullOrWhiteSpace(package.Path.Value) || !fileSystem.FileExists(package.Path.Value))
                        {
                            log.Verbose($"Package at {package.Path} not found.");
                        }
                        else
                        {
                            log.Verbose($"Removing package file '{package.Path}'");
                            fileSystem.DeleteFile(package.Path.Value, FailureOptions.IgnoreFailure);
                        }

                        journalRepository.RemovePackageEntry(package);
                    }
                    journalRepository.Commit();
                }
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }
        }

        public void ExpireStaleLocks(TimeSpan timeBeforeExpiration)
        {
            try
            {
                using (AcquireSemaphore())
                {
                    journalRepository.Load();
                    foreach (var entry in journalRepository.GetAllJournalEntries())
                    {
                        var locks = entry.GetLockDetails();
                        var staleLocks = locks
                            .Where(u => u.DateTime.Add(timeBeforeExpiration) <= DateTime.Now)
                            .ToList();

                        foreach (var staleLock in staleLocks)
                        {
                            entry.RemoveLock(staleLock.DeploymentTaskId);
                        }
                    }
                    journalRepository.Commit();
                }
            }
            catch (Exception ex)
            {
                log.Info($"Unable to expire stale package locks.{Environment.NewLine}{ex.ToString()}");
            }
        }

        IDisposable AcquireSemaphore()
        {
            return semaphoreFactory.Acquire(nameof(PackageJournal), "Another process is using the package journal");
        }
    }
}