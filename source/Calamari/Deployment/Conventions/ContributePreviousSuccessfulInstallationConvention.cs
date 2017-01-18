using Calamari.Deployment.Journal;

namespace Calamari.Deployment.Conventions
{
    public class ContributePreviousSuccessfulInstallationConvention : IInstallConvention
    {
        readonly IDeploymentJournal journal;

        public ContributePreviousSuccessfulInstallationConvention(IDeploymentJournal journal)
        {
            this.journal = journal;
        }

        public void Install(RunningDeployment deployment)
        {
            var policySet = deployment.Variables.Get(SpecialVariables.RetentionPolicySet);
            var previous = journal.GetLatestSuccessfulInstallation(policySet);
            string previousExtractedFrom;
            string previousExtractedTo;
            string previousVersion;
            string previousCustom;
            if (previous == null)
            {
                previousExtractedTo = previousExtractedFrom = previousVersion = previousCustom = "";
            }
            else
            {
                previousExtractedFrom = previous.ExtractedFrom;
                previousExtractedTo = previous.ExtractedTo;
                previousVersion = previous.PackageVersion;
                previousCustom = previous.CustomInstallationDirectory;
            }

            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath, previousExtractedTo);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory, previousCustom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageFilePath, previousExtractedFrom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageVersion, previousVersion);
        }
    }
}