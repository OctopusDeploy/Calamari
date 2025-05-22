using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.Packages.Download;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PackageCacheCleaner : IRetentionAlgorithm
    {
        const string PackageRetentionPercentFreeDiskSpace = "OctopusPackageRetentionPercentFreeDiskSpace";
        const string MachinePackageCacheRetentionQuantityToKeep = nameof(MachinePackageCacheRetentionQuantityToKeep);
        const int DefaultPercentFreeDiskSpace = 20;
        const int FreeSpacePercentBuffer = 30;
        readonly ISortJournalEntries sortJournalEntries;
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageDownloaderUtils packageUtils = new PackageDownloaderUtils();

        public PackageCacheCleaner(ICalamariFileSystem fileSystem, ISortJournalEntries sortJournalEntries, IVariables variables, ILog log)
        {
            this.fileSystem = fileSystem;
            this.sortJournalEntries = sortJournalEntries;
            this.variables = variables;
            this.log = log;
        }

        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries)
        {
            return OctopusFeatureToggles.ConfigurablePackageCacheRetentionFeatureToggle.IsEnabled(variables) ? FindPackagesToRemoveByQuantityToKeep(journalEntries) : FindPackagesToRemoveByPercentFreeDiskSpace(journalEntries);
        }

        IEnumerable<PackageIdentity> FindPackagesToRemoveByPercentFreeDiskSpace(IEnumerable<JournalEntry> journalEntries)
        {
            if (!fileSystem.GetDiskFreeSpace(packageUtils.RootDirectory, out var totalNumberOfFreeBytes) || !fileSystem.GetDiskTotalSpace(packageUtils.RootDirectory, out var totalNumberOfBytes))
            {
                log.Info("Unable to determine disk space. Skipping free space package retention.");
                return new PackageIdentity[0];
            }

            var percentFreeDiskSpaceDesired = variables.GetInt32(PackageRetentionPercentFreeDiskSpace) ?? DefaultPercentFreeDiskSpace;
            var desiredSpaceInBytes = totalNumberOfBytes * (ulong) percentFreeDiskSpaceDesired / 100;
            if (totalNumberOfFreeBytes > desiredSpaceInBytes)
            {
                log.VerboseFormat("Detected enough space for new packages. ({0}/{1})", totalNumberOfFreeBytes.ToFileSizeString(), totalNumberOfBytes.ToFileSizeString());
                return new PackageIdentity[0];
            }

            var spaceToFree = (desiredSpaceInBytes - totalNumberOfFreeBytes) * (100 + FreeSpacePercentBuffer) / 100;
            log.VerboseFormat("Cleaning {0} space from the package cache.", spaceToFree.ToFileSizeString());
            ulong spaceFreed = 0L;
            var orderedJournalEntries = sortJournalEntries.Sort(journalEntries);
            return orderedJournalEntries.TakeWhile(entry =>
                                                   {
                                                       var moreToClean = spaceFreed < spaceToFree;
                                                       spaceFreed += entry.FileSizeBytes;
                                                       return moreToClean;
                                                   })
                                        .Select(entry => entry.Package);
        }

        IEnumerable<PackageIdentity> FindPackagesToRemoveByQuantityToKeep(IEnumerable<JournalEntry> journalEntries)
        {
            var journals = journalEntries.ToArray();
            var quantityToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityToKeep);

            if (quantityToKeep == null || quantityToKeep == 0)
            {
                log.Verbose("Maximum quantity to keep is not configured. No packages will be removed.");
                return Array.Empty<PackageIdentity>();
            }

            if (journals.Length <= quantityToKeep)
            {
                log.Verbose("Cache size is less than the maximum quantity to keep. No packages will be removed.");
                return Array.Empty<PackageIdentity>();
            }

            var orderedJournalEntries = JournalEntrySorter.LeastRecentlyUsed(journals).ToArray();
            var numberOfPackagesToRemove = (int)(orderedJournalEntries.Length - quantityToKeep);
            
            log.VerboseFormat("Cache size is greater than the maximum quantity to keep. {0} packages will be removed.", numberOfPackagesToRemove);

            return orderedJournalEntries.Take(numberOfPackagesToRemove)
                                        .Select(entry => entry.Package);
        }
    }
}