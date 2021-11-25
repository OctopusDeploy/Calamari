using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;
using Octopus.Versioning;

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
                        entry.AddUsage(deploymentTaskId, age); //TODO: fix this to use the actual age.
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

        public void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId)
        {
            try
            {
                using (var repository = repositoryFactory.CreateJournalRepository())
                {
                    if (repository.TryGetJournalEntry(package, out var entry))
                    {
                        entry.RemoveLock(serverTaskId);
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
                       && entry.HasLock();
            }
        }

        public bool TryGetVersionFormat(PackageId packageId, string version, out VersionFormat versionFormat)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                var format = repository.GetJournalEntries(packageId).FirstOrDefault()?.Package?.Version?.Format;
                versionFormat = format ?? VersionFormat.Semver;

                return format != null;
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

        public bool TryGetVersionFormat(PackageId packageId, ServerTaskId deploymentTaskID, out VersionFormat? format)
        {
            //We can call this if we don't know the package version format from variables - if this isn't the first time this package has been references by this server task, then we should be able to get it from an earlier use (eg from FindPackageCommand)
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                return repository.GetJournalEntries(packageId, deploymentTaskID)
                                 .TryGetFirstValidVersionFormat(out format);
            }
        }

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Age is the number of other packages which have been registered or accessed since this one.
        /// </summary>
        public int GetPackageAge(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                //TODO: fail if not found?
                return repository.TryGetJournalEntry(package, out var journalEntry) ? journalEntry.GetUsageDetails().Max(m => m.CacheAge.Value) : int.MinValue;
            }
        }

        public int GetNewerVersionCount(PackageIdentity package)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                var allEntries = repository.GetJournalEntries(package.PackageId);

                return allEntries.Count(e => e.Package.Version.CompareTo(package.Version) == 1);

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