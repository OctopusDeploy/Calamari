using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.Packages.Download;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PercentFreeDiskSpacePackageCleaner : IRetentionAlgorithm
    {
        const string PackageRetentionPercentFreeDiskSpace = "OctopusPackageRetentionPercentFreeDiskSpace";
        const int DefaultPercentFreeDiskSpace = 20;
        const int FreeSpacePercentBuffer = 30;
        readonly ISortJournalEntries sortJournalEntries;
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageDownloaderUtils packageUtils = new PackageDownloaderUtils();

        public PercentFreeDiskSpacePackageCleaner(ICalamariFileSystem fileSystem, ISortJournalEntries sortJournalEntries, IVariables variables, ILog log)
        {
            this.fileSystem = fileSystem;
            this.sortJournalEntries = sortJournalEntries;
            this.variables = variables;
            this.log = log;
        }

        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries)
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
            var allPackageJournalEntries = journalEntries.ToList();
            var orderedJournalEntries = sortJournalEntries.Sort(allPackageJournalEntries);
            var packages = orderedJournalEntries.TakeWhile(entry =>
                                        {
                                            var moreToClean = spaceFreed < spaceToFree;
                                            spaceFreed += entry.FileSizeBytes;
                                            return moreToClean;
                                        })
                                        .Select(entry => entry.Package)
                                        .ToList();
            if (spaceFreed < spaceToFree)
            {
                if (spaceFreed == 0 && packages.Count == 0)
                {
                    log.Warn($"No packages can be deleted from a total of {allPackageJournalEntries.Count} packages.");
                }
                else
                {
                    log.Warn($"Only {spaceFreed.ToFileSizeString()} from {packages.Count} packages can be deleted from a total of {allPackageJournalEntries.Count} packages. ");
                }
                log.Warn("This means there will be less free space than desired. This could happen if packages are still being used by other deployments and cannot be currently be deleted.");
            }
            else
            {
                log.Verbose($"{spaceFreed.ToFileSizeString()} has been freed by deleting {packages.Count} packages");
            }
            return packages;
        }
    }
}