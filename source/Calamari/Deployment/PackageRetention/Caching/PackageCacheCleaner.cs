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
        const string MachinePackageCacheRetentionQuantityOfPackagesToKeep = nameof(MachinePackageCacheRetentionQuantityOfPackagesToKeep);
        const string MachinePackageCacheRetentionQuantityOfVersionsToKeep = nameof(MachinePackageCacheRetentionQuantityOfVersionsToKeep);
        const string PackageUnit = "MachinePackageCacheRetentionPackageUnit";
        const string VersionUnit = "MachinePackageCacheRetentionVersionUnit";
        const int DefaultQuantityOfPackagesToKeep = 0;
        const int DefaultQuantityOfVersionsToKeep = 0;
        const MachinePackageCacheRetentionUnit DefaultPackageUnit = MachinePackageCacheRetentionUnit.Items;
        const MachinePackageCacheRetentionUnit DefaultVersionUnit = MachinePackageCacheRetentionUnit.Items;
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
            var quantityOfPackagesToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfPackagesToKeep) ?? DefaultQuantityOfPackagesToKeep;
            var quantityOfVersionsToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfVersionsToKeep) ?? DefaultQuantityOfVersionsToKeep;
            
            var packageUnitString = variables.Get(PackageUnit);
            var versionUnitString = variables.Get(VersionUnit);
            var packageUnit = packageUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), packageUnitString) : DefaultPackageUnit;
            var versionUnit = versionUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), versionUnitString) : DefaultVersionUnit;

            if (quantityOfVersionsToKeep == 0)
            {
                return FindPackagesToRemoveByPercentFreeDiskSpace(journals);
            }

            var orderedJournalEntries = JournalEntrySorter.MostRecentlyUsedByVersion(journals).ToArray();
            
            var packagesToRemoveById = new List<PackageIdentity>();

            // Package retention has currently only been implemented for number of items
            if (packageUnit == MachinePackageCacheRetentionUnit.Items && quantityOfPackagesToKeep > 0 && quantityOfPackagesToKeep < orderedJournalEntries.Length)
            {
                log.VerboseFormat("Cache size is greater than the maximum quantity to keep. {0} packages will be removed.", orderedJournalEntries.Length - quantityOfPackagesToKeep);

                packagesToRemoveById = orderedJournalEntries.Skip(quantityOfPackagesToKeep)
                                                            .SelectMany(entry => entry.Value.Select(v => v.Package)).ToList();
            }
            
            var packagesToRemoveByVersion = new List<PackageIdentity>();

            // Version retention has currently only been implemented for number of items
            if (versionUnit == MachinePackageCacheRetentionUnit.Items && quantityOfVersionsToKeep > 0)
            {
                foreach (var packageToKeep in orderedJournalEntries)
                {
                    packagesToRemoveByVersion.AddRange(JournalEntrySorter.MostRecentlyUsed(packageToKeep.Value).Skip(quantityOfVersionsToKeep).Select(v => v.Package));
                }
            }
            
            if (packagesToRemoveByVersion.Any())
            {
                log.VerboseFormat("Found cached packages with more versions than the maximum quantity to keep. {0} package versions will be removed.", packagesToRemoveByVersion.Count);
            }

            return packagesToRemoveById.Union(packagesToRemoveByVersion);
        }
    }
    
    public enum MachinePackageCacheRetentionUnit
    {
        Items
    }
}