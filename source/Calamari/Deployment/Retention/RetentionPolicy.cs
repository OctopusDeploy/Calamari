using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Time;

namespace Calamari.Deployment.Retention
{
    public class RetentionPolicy : IRetentionPolicy
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IDeploymentJournal deploymentJournal;
        readonly IClock clock;

        public RetentionPolicy(ICalamariFileSystem fileSystem, IDeploymentJournal deploymentJournal, IClock clock)
        {
            this.fileSystem = fileSystem;
            this.deploymentJournal = deploymentJournal;
            this.clock = clock;
        }

        public void ApplyRetentionPolicy(string retentionPolicySet, int? days, int? releases)
        {
            var deployments = deploymentJournal
                .GetAllJournalEntries()
                .Where(x => x.RetentionPolicySet == retentionPolicySet)
                .ToList();
            var preservedEntries = new List<JournalEntry>();

            if (days.HasValue && days.Value > 0)
            {
                deployments = deployments
                    .Where(InstalledBeforePolicyDayCutoff(days.Value, preservedEntries))
                    .ToList();
            }
            else if (releases.HasValue && releases.Value > 0)
            {
                // Keep the current release, plus specified releases value
                // Unsuccessful releases are not included in the count of releases to keep
                deployments = deployments
                    .OrderByDescending(deployment => deployment.InstalledOn)
                    .SkipWhile(SuccessfulCountLessThanPolicyCount(releases.Value, preservedEntries))
                    .ToList();
            }

            if (!deployments.Any())
            {
                Log.Info("Did not find any deployments to clean up");
            }

            foreach (var deployment in deployments)
            {
                DeleteExtractionDestination(deployment, preservedEntries);
                DeleteExtractionSource(deployment, preservedEntries);
            }
            deploymentJournal.RemoveJournalEntries(deployments.Select(x => x.Id));
        }

        void DeleteExtractionSource(JournalEntry deployment, List<JournalEntry> preservedEntries)
        {
            if (string.IsNullOrWhiteSpace(deployment.ExtractedFrom) 
                || !fileSystem.FileExists(deployment.ExtractedFrom) 
                || preservedEntries.Any(entry => deployment.ExtractedFrom.Equals(entry.ExtractedFrom, StringComparison.Ordinal)))
                return;

            Log.Info($"Removing package file '{deployment.ExtractedFrom}'");
            fileSystem.DeleteFile(deployment.ExtractedFrom, FailureOptions.IgnoreFailure);
        }

        void DeleteExtractionDestination(JournalEntry deployment, List<JournalEntry> preservedEntries)
        {
            if (!fileSystem.DirectoryExists(deployment.ExtractedTo) 
                || preservedEntries.Any(entry => deployment.ExtractedTo.Equals(entry.ExtractedTo, StringComparison.Ordinal)))
                return;

            Log.Info($"Removing directory '{deployment.ExtractedTo}'");
            fileSystem.PurgeDirectory(deployment.ExtractedTo, FailureOptions.IgnoreFailure);

            try
            {
                fileSystem.DeleteDirectory(deployment.ExtractedTo);
            }
            catch (Exception ex)
            {
                Log.VerboseFormat("Could not delete directory '{0}' because some files could not be deleted: {1}",
                    deployment.ExtractedFrom, ex.Message);
            }
        }

        static Func<JournalEntry, bool> SuccessfulCountLessThanPolicyCount(int releases, List<JournalEntry> preservedEntries)
        {
            return journalEntry =>
            {
                if (preservedEntries.Count() > releases)
                {
                    Log.Verbose($"Keeping {journalEntry.ExtractedTo} and {journalEntry.ExtractedFrom} as it was one of the last {releases+1} successful releases");
                    return false;
                }

                if (journalEntry.WasSuccessful)
                    preservedEntries.Add(journalEntry);

                return true;
            };
        }

        Func<JournalEntry, bool> InstalledBeforePolicyDayCutoff(int days, List<JournalEntry> preservedEntries)
        {
            return journalEntry =>
            {
                if (journalEntry.InstalledOn < clock.GetUtcTime().AddDays(-days))
                    return true;

                Log.Verbose($"Keeping {journalEntry.ExtractedTo} and {journalEntry.ExtractedFrom} as it was installed less than {days} days ago");

                preservedEntries.Add(journalEntry);
                return false;
            };
        }
    }
}