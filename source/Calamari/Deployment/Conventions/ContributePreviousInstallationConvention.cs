using Calamari.Deployment.Journal;
using Calamari.Extensibility;

namespace Calamari.Deployment.Conventions
{
    public class ContributePreviousInstallationConvention : IInstallConvention
    {
        readonly IDeploymentJournal journal;

        public ContributePreviousInstallationConvention(IDeploymentJournal journal)
        {
            this.journal = journal;
        }

        public void Install(RunningDeployment deployment)
        {
            var policySet = deployment.Variables.Get(SpecialVariables.RetentionPolicySet);
            var previous = journal.GetLatestInstallation(policySet);
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

            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousInstallation.OriginalInstalledPath, previousExtractedTo);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousInstallation.CustomInstallationDirectory, previousCustom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageFilePath, previousExtractedFrom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageVersion, previousVersion);
        }
    }
}