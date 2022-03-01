using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.Packages.Download;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FreeDiskSpacePackageCleaner : IRetentionAlgorithm
    {
        readonly IOrderJournalEntries orderJournalEntries;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageDownloaderUtils packageUtils = new PackageDownloaderUtils();

        public FreeDiskSpacePackageCleaner(ICalamariFileSystem fileSystem, IOrderJournalEntries orderJournalEntries, ILog log)
        {
            this.fileSystem = fileSystem;
            this.orderJournalEntries = orderJournalEntries;
            this.log = log;
        }

        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries)
        {
            if (!fileSystem.GetDiskFreeSpace(packageUtils.RootDirectory, out var totalNumberOfFreeBytes) || !fileSystem.GetDiskTotalSpace(packageUtils.RootDirectory, out var totalNumberOfBytes))
            {
                log.Info("Unable to determine disk space. Package retention will not run.");
                return new PackageIdentity[0];
            }

            var twentyPercentOfDisk = totalNumberOfBytes * 0.2;
            if (totalNumberOfFreeBytes > twentyPercentOfDisk)
            {
                log.Verbose("Detected enough space for new packages.");
                return new PackageIdentity[0];
            }

            var spaceRequired = twentyPercentOfDisk - totalNumberOfFreeBytes;
            var spaceFreed = 0L;
            var orderedJournalEntries = orderJournalEntries.Order(journalEntries);
            return orderedJournalEntries.TakeWhile(entry =>
                                                   {
                                                       spaceFreed += entry.FileSizeBytes;
                                                       return spaceFreed < spaceRequired;
                                                   })
                                        .Select(entry => entry.Package);
        }
    }
}