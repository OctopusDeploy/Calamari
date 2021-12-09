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
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class Journal : IManagePackageUse
    {
        readonly IJournalRepositoryFactory repositoryFactory;
        readonly IRetentionAlgorithm retentionAlgorithm;
        readonly IVariables variables;
        readonly ILog log;

        public Journal(IJournalRepositoryFactory repositoryFactory, IVariables variables, IRetentionAlgorithm retentionAlgorithm, ILog log)
        {
            this.repositoryFactory = repositoryFactory;
            this.variables = variables;
            this.retentionAlgorithm = retentionAlgorithm;
            this.log = log;
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

        public int GetUsageCount(PackageIdentity package)
        {
            return GetUsage(package).Count();
        }

        public void ExpireStaleLocks()
        {
            throw new NotImplementedException();
        }

        public void ApplyRetention(long spaceNeeded)
        {
            using (var repository = repositoryFactory.CreateJournalRepository())
            {
                var journalEntries = repository.GetAllJournalEntries();
                var packagesToRemove = retentionAlgorithm.GetPackagesToRemove(journalEntries, spaceNeeded);
                //TODO: implement this.
            }
        }
    }
}