using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
using Calamari.Integration.Time;

namespace Calamari.Deployment.Retention
{
    public class RetentionPolicy : IRetentionPolicy
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        readonly ICalamariFileSystem fileSystem;
        readonly IDeploymentJournal deploymentJournal;
        readonly IClock clock;
        readonly ILog log;

        public RetentionPolicy(ICalamariFileSystem fileSystem, IDeploymentJournal deploymentJournal, IClock clock, ILog log)
        {
            this.fileSystem = fileSystem;
            this.deploymentJournal = deploymentJournal;
            this.clock = clock;
            this.log = log;
        }

        public void ApplyRetentionPolicy(string retentionPolicySet, int? daysToKeep, int? successfulDeploymentsToKeep)
        {
            var deploymentsToDelete = deploymentJournal
                .GetAllJournalEntries()
                .Where(x => x.RetentionPolicySet == retentionPolicySet)
                .ToList();
            var preservedEntries = new List<JournalEntry>();

            if (daysToKeep.HasValue && daysToKeep.Value > 0)
            {
                log.Info($"Keeping deployments from the last {daysToKeep} days");
                deploymentsToDelete = deploymentsToDelete
                    .Where(InstalledBeforePolicyDayCutoff(daysToKeep.Value, preservedEntries))
                    .ToList();
            }
            else if (successfulDeploymentsToKeep.HasValue && successfulDeploymentsToKeep.Value > 0)
            {
                log.Info($"Keeping this deployment and the previous {successfulDeploymentsToKeep} successful deployments");
                // Keep the current deployment, plus specified deployment value
                // Unsuccessful deployments are not included in the count of deployment to keep

                deploymentsToDelete = deploymentsToDelete
                              .OrderByDescending(deployment => deployment.InstalledOn)
                              .Where(SuccessfulCountGreaterThanPolicyCountOrDeployedUnsuccessfully(successfulDeploymentsToKeep.Value, preservedEntries))
                              .ToList();
                if (preservedEntries.Count <= successfulDeploymentsToKeep) deploymentsToDelete = new List<JournalEntry>();
            }
            else
            {
                log.Info("Keeping all deployments");
                return;
            }

            if (!deploymentsToDelete.Any())
            {
                log.Info("Did not find any deployments to clean up");
            }

            var entriesRemoved = RemoveDeployments(deploymentsToDelete, preservedEntries);
            deploymentJournal.RemoveJournalEntries(entriesRemoved);

            RemovedFailedPackageDownloads();
        }

        string[] RemoveDeployments(List<JournalEntry> deploymentsToDelete, List<JournalEntry> preservedEntries)
        {
            var deploymentsDeleted = new List<string>();
            foreach (var deployment in deploymentsToDelete)
            {
                try
                {
                    DeleteExtractionDestination(deployment, preservedEntries);
                    deploymentsDeleted.Add(deployment.Id);
                }
                catch (Exception ex)
                {
                    log.VerboseFormat("Could not delete directory '{0}' because some files could not be deleted: {1}",
                                   deployment.ExtractedTo,
                                   ex.Message);
                }
            }

            return deploymentsDeleted.ToArray();
        }

        void DeleteExtractionDestination(JournalEntry deployment, List<JournalEntry> preservedEntries)
        {
            if (string.IsNullOrWhiteSpace(deployment.ExtractedTo)
                || !fileSystem.DirectoryExists(deployment.ExtractedTo)
                || preservedEntries.Any(entry => deployment.ExtractedTo.Equals(entry.ExtractedTo, StringComparison.Ordinal)))
                return;

            log.Info($"Removing directory '{deployment.ExtractedTo}'");
            fileSystem.PurgeDirectory(deployment.ExtractedTo, FailureOptions.IgnoreFailure);

            fileSystem.DeleteDirectory(deployment.ExtractedTo);
        }

        Func<JournalEntry, bool> SuccessfulCountGreaterThanPolicyCountOrDeployedUnsuccessfully(int successfulDeploymentsToKeep, List<JournalEntry> preservedEntries)
        {
            return journalEntry =>
            {
                if (journalEntry.WasSuccessful && preservedEntries.Count <= successfulDeploymentsToKeep)
                {
                    preservedEntries.Add(journalEntry);

                    var preservedDirectories = (!string.IsNullOrEmpty(journalEntry.ExtractedTo)
                            ? new List<string> { journalEntry.ExtractedTo }
                            : new List<string>())
                        .Concat(journalEntry.Packages.Select(p => p.DeployedFrom).Where(d => !string.IsNullOrEmpty(d)))
                        .ToList();

                    log.Verbose($"Keeping {FormatList(preservedDirectories)} as it is the {FormatWithThPostfix(preservedEntries.Count)}most recent successful release");

                    return false;
                }

                return true;
            };
        }

        Func<JournalEntry, bool> InstalledBeforePolicyDayCutoff(int days, List<JournalEntry> preservedEntries)
        {
            return journalEntry =>
            {
                var installedAgo = (clock.GetUtcTime() - journalEntry.InstalledOn);

                if (journalEntry.InstalledOn == null)
                    return false;
                if (installedAgo?.TotalDays > days)
                    return true;

                var preservedDirectories = (!string.IsNullOrEmpty(journalEntry.ExtractedTo)
                        ? new List<string> { journalEntry.ExtractedTo }
                        : new List<string>())
                    .Concat(journalEntry.Packages.Select(p => p.DeployedFrom).Where(p => !string.IsNullOrEmpty(p)))
                    .ToList();

                log.Verbose($"Keeping {FormatList(preservedDirectories)} as it was installed {installedAgo?.Days} days and {installedAgo?.Hours} hours ago");

                preservedEntries.Add(journalEntry);
                return false;
            };
        }

        static string FormatList(IList<string> items)
        {
            if (items.Count <= 1)
                return $"{items.FirstOrDefault() ?? ""}";

            return string.Join(", ", items.Take(items.Count - 1)) + $" and {items[items.Count - 1]}";
        }

        static string FormatWithThPostfix(int value)
        {
            if (value == 1)
                return "";

            if (value == 11 || value == 12 || value == 13)
                return value + "th ";

            switch (value % 10)
            {
                case 1:
                    return value + "st ";
                case 2:
                    return value + "nd ";
                case 3:
                    return value + "rd ";
                default:
                    return value + "th ";
            }
        }

        void RemovedFailedPackageDownloads()
        {
            var pattern = "*" + NuGetPackageDownloader.DownloadingExtension;

            if (fileSystem.DirectoryExists(PackageDownloaderUtils.RootDirectory))
            {
                var toDelete = fileSystem.EnumerateFilesRecursively(PackageDownloaderUtils.RootDirectory, pattern)
                    .Where(f => fileSystem.GetCreationTime(f) <= DateTime.Now.AddDays(-1))
                    .ToArray();

                foreach (var file in toDelete)
                {
                    log.Verbose($"Removing the failed to download file {file}");
                    fileSystem.DeleteFile(file, FailureOptions.IgnoreFailure);
                }
            }
        }
    }
}