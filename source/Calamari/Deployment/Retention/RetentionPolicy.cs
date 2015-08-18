using System;
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
            var deployments = deploymentJournal.GetAllJournalEntries().Where(x => x.RetentionPolicySet == retentionPolicySet);

            if (days.HasValue && days.Value > 0)
            {
                deployments = deployments.Where(x => x.InstalledOn < clock.GetUtcTime().AddDays(-days.Value)).ToList();
            }
            else if (releases.HasValue && releases.Value > 0)
            {
                var skipped = 0;

                // Keep the current release, plus specified releases value
                // Unsuccessful releases are not included in the count of releases to keep
                deployments = deployments
                    .OrderByDescending(x => x.InstalledOn)
                    .SkipWhile((x) => (x.WasSuccessful ? skipped++ : skipped) <= releases.Value)
                    .ToList();
            }

            foreach (var deployment in deployments)
            {
                if (fileSystem.DirectoryExists(deployment.ExtractedTo))
                {
                    Log.VerboseFormat("Removing directory '{0}'", deployment.ExtractedTo);
                    fileSystem.PurgeDirectory(deployment.ExtractedTo, FailureOptions.IgnoreFailure);

                    try
                    {
                        fileSystem.DeleteDirectory(deployment.ExtractedTo);
                    }
                    catch (Exception ex)
                    {
                        Log.VerboseFormat("Could not delete directory '{0}' because some files could not be deleted: {1}", deployment.ExtractedFrom, ex.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(deployment.ExtractedFrom) && fileSystem.FileExists(deployment.ExtractedFrom))
                {
                    Log.VerboseFormat("Removing package file '{0}'", deployment.ExtractedFrom);
                    fileSystem.DeleteFile(deployment.ExtractedFrom, FailureOptions.IgnoreFailure);
                }
            }

            deploymentJournal.RemoveJournalEntries(deployments.Select(x => x.Id));
        }
    }
}